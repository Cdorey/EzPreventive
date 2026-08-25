using EzNutrition.Presentation.Models;
using EzNutrition.Shared.Data.DTO;
using EzNutrition.Shared.Data.Entities;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;

namespace EzNutrition.Presentation.Services;

/// <summary>
/// 管理当前客户端进程中的身份、登录流程与公共系统信息。
/// </summary>
public sealed class UserSessionService : AuthenticationStateProvider
{
    private static readonly TimeSpan LoginRetryDelay = TimeSpan.FromMilliseconds(250);

    private readonly HttpClient client;
    private readonly ILoginCredentialStore credentialStore;
    private readonly object initializationLock = new();
    private readonly ILogger<UserSessionService> logger;
    private Task? initializationTask;
    private UserInfo? userInfo;

    /// <summary>
    /// 创建用户会话服务。
    /// </summary>
    /// <param name="httpClientFactory">用于创建匿名登录与系统信息客户端的工厂。</param>
    /// <param name="logger">用于记录不包含凭据内容的运行故障。</param>
    /// <param name="credentialStore">宿主可选提供的安全登录信息存储。</param>
    /// <param name="clientVersion">测试或特殊宿主可显式提供的前端产品版本。</param>
    public UserSessionService(
        IHttpClientFactory httpClientFactory,
        ILogger<UserSessionService> logger,
        ILoginCredentialStore? credentialStore = null,
        string? clientVersion = null)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        client = httpClientFactory.CreateClient("Anonymous");
        this.credentialStore = credentialStore ?? UnavailableLoginCredentialStore.Instance;
        ClientVersion = NormalizeOptional(clientVersion);
        if (ClientVersion.Length == 0)
        {
            ClientVersion = ResolveClientVersion();
        }
    }

    /// <summary>在会话可见状态变化时通知 Razor 页面。</summary>
    public event Action? StateChanged;

    /// <summary>获取当前已登录用户；匿名或令牌过期时返回 <see langword="null"/>。</summary>
    public UserInfo? UserInfo
    {
        get => userInfo;
        private set
        {
            if (!ReferenceEquals(userInfo, value))
            {
                userInfo = value;
                NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
                StateChanged?.Invoke();
            }
        }
    }

    /// <summary>获取备案编号。</summary>
    public string CaseNumber { get; private set; } = string.Empty;

    /// <summary>获取当前运行的前端宿主产品发行版本。</summary>
    public string ClientVersion { get; }

    /// <summary>获取当前连接的服务端产品发行版本。</summary>
    public string ServerVersion { get; private set; } = string.Empty;

    /// <summary>获取前后端产品代际或接口契约代际是否不一致。</summary>
    public bool HasVersionCompatibilityWarning =>
        IsCompatibilityMismatch(ClientVersion, ServerVersion);

    /// <summary>获取产品说明。</summary>
    public string CoverLetter { get; private set; } = string.Empty;

    /// <summary>获取工作提示。</summary>
    public string Notice { get; private set; } = string.Empty;

    /// <summary>获取公共系统信息是否已经完成首次加载。</summary>
    public bool IsSystemInfoLoaded { get; private set; }

    /// <summary>获取或设置当前工作台是否检测到触摸输入。</summary>
    public bool IsTouchDetected { get; set; }

    /// <summary>获取当前宿主是否支持安全保存登录信息。</summary>
    public bool CanRememberLogin => credentialStore.IsAvailable;

    /// <summary>获取是否正在尝试使用宿主保存的信息自动登录。</summary>
    public bool IsAutomaticSignInInProgress { get; private set; }

    /// <summary>获取自动登录失败后可向用户解释的消息。</summary>
    public string? AutomaticSignInError { get; private set; }

    /// <summary>获取保存或清除登录信息失败时的警告。</summary>
    public string? CredentialPersistenceWarning { get; private set; }

    /// <summary>获取自动登录失败后建议保留在登录表单中的用户名。</summary>
    public string? SuggestedUserName { get; private set; }

    /// <summary>
    /// 首次初始化公共系统信息，并在宿主支持时尝试自动登录。
    /// </summary>
    /// <remarks>同一会话中的并发调用共享一次初始化任务。</remarks>
    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Task currentInitialization;
        lock (initializationLock)
        {
            currentInitialization = initializationTask ??= InitializeCoreAsync();
        }

        return cancellationToken.CanBeCanceled
            ? currentInitialization.WaitAsync(cancellationToken)
            : currentInitialization;
    }

    /// <summary>从服务端加载不需要身份认证的公共系统信息。</summary>
    public async Task GetSystemInfoAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var publicInfoTask = TryGetPublicSystemInfoAsync(cancellationToken);
            var coverLetterTask = TryGetNoticeAsync("SystemInfo/CoverLetter/", cancellationToken);
            var noticeTask = TryGetNoticeAsync("SystemInfo/Notice/", cancellationToken);

            await Task.WhenAll(publicInfoTask, coverLetterTask, noticeTask);

            var publicInfo = await publicInfoTask;
            CaseNumber = NormalizeOptional(publicInfo.CaseNumber);
            ServerVersion = NormalizeOptional(publicInfo.ServerVersion);
            CoverLetter = await coverLetterTask;
            Notice = await noticeTask;
        }
        finally
        {
            IsSystemInfoLoaded = true;
            StateChanged?.Invoke();
        }
    }

    /// <summary>尝试取得仍在有效期内的当前访问令牌。</summary>
    public bool TryGetAccessToken(out string token)
    {
        var currentUser = UserInfo;
        if (currentUser is null)
        {
            token = string.Empty;
            return false;
        }

        if (currentUser.IsExpired)
        {
            UserInfo = null;
            token = string.Empty;
            return false;
        }

        token = currentUser.Token;
        return !string.IsNullOrWhiteSpace(token);
    }

    /// <inheritdoc />
    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var currentUser = UserInfo;
        ClaimsPrincipal userPrincipal = currentUser is not null && !currentUser.IsExpired
            ? new ClaimsPrincipal(new ClaimsIdentity(
                currentUser.Claims,
                "jwt",
                ClaimTypes.Name,
                ClaimTypes.Role))
            : new ClaimsPrincipal(new ClaimsIdentity());
        return Task.FromResult(new AuthenticationState(userPrincipal));
    }

    /// <summary>
    /// 使用用户名和密码登录，但不要求宿主持久化登录信息。
    /// </summary>
    public Task SignInAsync(
        string userName,
        string password,
        CancellationToken cancellationToken = default) =>
        SignInAsync(userName, password, rememberLogin: false, cancellationToken);

    /// <summary>
    /// 使用用户名和密码登录，并按用户选择保存或清除当前连接的登录信息。
    /// </summary>
    public async Task SignInAsync(
        string userName,
        string password,
        bool rememberLogin,
        CancellationToken cancellationToken = default)
    {
        var normalizedUserName = ValidateLoginInput(userName, password);
        var signedInUser = await AuthenticateAsync(normalizedUserName, password, cancellationToken);

        AutomaticSignInError = null;
        SuggestedUserName = null;
        CredentialPersistenceWarning = null;
        if (credentialStore.IsAvailable)
        {
            try
            {
                if (rememberLogin)
                {
                    await credentialStore.SaveAsync(
                        new SavedLoginCredential(normalizedUserName, password),
                        cancellationToken);
                }
                else
                {
                    await credentialStore.ClearAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "The host could not update its saved login credential.");
                CredentialPersistenceWarning = rememberLogin
                    ? "登录成功，但 Windows 无法安全保存登录信息；下次启动仍需重新登录。"
                    : "登录成功，但 Windows 无法确认旧的登录信息已经清除，请在设置中重试清除。";
            }
        }

        UserInfo = signedInUser;
        StateChanged?.Invoke();
    }

    /// <summary>
    /// 注销当前进程会话，并可同时清除宿主保存的登录信息。
    /// </summary>
    /// <param name="forgetSavedLogin">
    /// 显式退出时应为 <see langword="true"/>；仅因访问令牌失效而结束进程会话时可为
    /// <see langword="false"/>，以便下次启动重新向服务端换取短期令牌。
    /// </param>
    public async Task SignOutAsync(
        bool forgetSavedLogin = true,
        CancellationToken cancellationToken = default)
    {
        UserInfo = null;
        AutomaticSignInError = null;
        SuggestedUserName = null;
        CredentialPersistenceWarning = null;

        if (forgetSavedLogin && credentialStore.IsAvailable)
        {
            try
            {
                await credentialStore.ClearAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "The host could not clear its saved login credential.");
                CredentialPersistenceWarning = "已经退出当前会话，但 Windows 无法清除保存的登录信息，请在设置中重试。";
                StateChanged?.Invoke();
            }
        }
    }

    private async Task InitializeCoreAsync()
    {
        var systemInfoTask = GetSystemInfoAsync();
        var automaticSignInTask = TryAutomaticSignInAsync();
        await Task.WhenAll(systemInfoTask, automaticSignInTask);
    }

    private async Task TryAutomaticSignInAsync()
    {
        if (!credentialStore.IsAvailable)
        {
            return;
        }

        IsAutomaticSignInInProgress = true;
        StateChanged?.Invoke();
        try
        {
            SavedLoginCredential? credential;
            try
            {
                credential = await credentialStore.ReadAsync();
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "The host could not read its saved login credential.");
                AutomaticSignInError = "无法读取 Windows 保存的登录信息，请手动登录。";
                return;
            }

            if (credential is null)
            {
                return;
            }

            SuggestedUserName = credential.UserName;
            try
            {
                UserInfo = await AuthenticateAsync(
                    credential.UserName,
                    credential.Password,
                    CancellationToken.None);
                SuggestedUserName = null;
                AutomaticSignInError = null;
            }
            catch (LoginRejectedException)
            {
                await TryClearRejectedCredentialAsync();
                AutomaticSignInError = "保存的登录信息已失效，请重新输入；本机副本已经清除。";
            }
            catch (InvalidOperationException exception)
            {
                AutomaticSignInError = $"自动登录失败：{exception.Message} 已保留本机登录信息，可稍后重试。";
            }
        }
        finally
        {
            IsAutomaticSignInInProgress = false;
            StateChanged?.Invoke();
        }
    }

    private async Task TryClearRejectedCredentialAsync()
    {
        try
        {
            await credentialStore.ClearAsync();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "The host could not clear a rejected saved login credential.");
            CredentialPersistenceWarning = "登录信息已被服务器拒绝，但 Windows 无法清除本机副本，请在设置中重试。";
        }
    }

    private async Task<UserInfo> AuthenticateAsync(
        string userName,
        string password,
        CancellationToken cancellationToken)
    {
        using var response = await PostLoginAsync(userName, password, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new LoginRejectedException(
                    "用户名或密码错误、邮箱尚未确认，或账户暂时被锁定。");
            }

            throw new InvalidOperationException("登录失败，请稍后重试。");
        }

        var token = await response.Content.ReadAsStringAsync(cancellationToken);
        try
        {
            var signedInUser = new UserInfo(token);
            if (signedInUser.IsExpired)
            {
                throw new InvalidOperationException("服务器返回的登录凭据已经过期。");
            }

            return signedInUser;
        }
        catch (ArgumentException exception)
        {
            logger.LogError(exception, "The login endpoint returned an invalid JWT.");
            throw new InvalidOperationException("服务器返回了无效的登录凭据，请稍后重试。", exception);
        }
    }

    private async Task<HttpResponseMessage> PostLoginAsync(
        string userName,
        string password,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await SendLoginRequestAsync(userName, password, cancellationToken);
            if (!IsTransientServerFailure(response.StatusCode))
            {
                return response;
            }

            logger.LogWarning(
                "Login endpoint returned transient HTTP status {StatusCode}; retrying once.",
                (int)response.StatusCode);
            response.Dispose();
            await Task.Delay(LoginRetryDelay, cancellationToken);
            return await SendLoginRequestAsync(userName, password, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Unable to reach the login endpoint.");
            throw new InvalidOperationException("无法连接服务器，请检查网络后重试。", exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "The login request timed out.");
            throw new InvalidOperationException("登录请求超时，请稍后重试。", exception);
        }
    }

    private async Task<HttpResponseMessage> SendLoginRequestAsync(
        string userName,
        string password,
        CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>(nameof(userName), userName),
            new KeyValuePair<string, string>(nameof(password), password)
        ]);

        return await client.PostAsync("Auth/Login", content, cancellationToken);
    }

    private static string ValidateLoginInput(string userName, string password)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(password))
        {
            throw new InvalidOperationException("请输入用户名和密码。");
        }

        return userName.Trim();
    }

    private static bool IsTransientServerFailure(HttpStatusCode statusCode) =>
        (int)statusCode is 500 or 502 or 503 or 504;

    internal static bool IsCompatibilityMismatch(
        string? clientVersion,
        string? serverVersion)
    {
        if (!Version.TryParse(clientVersion, out var parsedClientVersion) ||
            !Version.TryParse(serverVersion, out var parsedServerVersion))
        {
            return false;
        }

        return parsedClientVersion.Major != parsedServerVersion.Major ||
            parsedClientVersion.Minor != parsedServerVersion.Minor;
    }

    private static string ResolveClientVersion()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version
            ?? typeof(UserSessionService).Assembly.GetName().Version;
        return version?.ToString(4) ?? string.Empty;
    }

    private static string NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private async Task<PublicSystemInfoDto> TryGetPublicSystemInfoAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            return await client.GetFromJsonAsync<PublicSystemInfoDto>(
                "SystemInfo/PublicInfo/",
                cancellationToken)
                ?? new PublicSystemInfoDto(null, null);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or NotSupportedException or System.Text.Json.JsonException)
        {
            logger.LogWarning(exception, "Unable to load public server information.");
            return new PublicSystemInfoDto(null, null);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Loading public server information timed out.");
            return new PublicSystemInfoDto(null, null);
        }
    }

    private async Task<string> TryGetNoticeAsync(string requestUri, CancellationToken cancellationToken)
    {
        try
        {
            var notice = await client.GetFromJsonAsync<Notice>(requestUri, cancellationToken);
            return notice?.Description ?? string.Empty;
        }
        catch (Exception exception) when (
            exception is HttpRequestException or NotSupportedException or System.Text.Json.JsonException)
        {
            logger.LogWarning(exception, "Unable to load notice from {RequestUri}.", requestUri);
            return string.Empty;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Loading notice from {RequestUri} timed out.", requestUri);
            return string.Empty;
        }
    }

    private sealed class LoginRejectedException(string message) : InvalidOperationException(message);
}
