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
    private readonly HttpClient client;
    private readonly IAuthenticationSessionClient authentication;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim operationGate = new(1, 1);
    private readonly object stateLock = new();
    private readonly object initializationLock = new();
    private readonly ILogger<UserSessionService> logger;
    private Task? initializationTask;
    private UserInfo? userInfo;
    private AuthenticationTokensDto? tokens;
    private long generation;
    private DateTimeOffset retryRefreshAfter;
    private string? refreshFailure;

    /// <summary>
    /// 创建用户会话服务。
    /// </summary>
    /// <param name="httpClientFactory">用于创建匿名登录与系统信息客户端的工厂。</param>
    /// <param name="logger">用于记录不包含凭据内容的运行故障。</param>
    /// <param name="authentication">宿主认证及刷新凭据管理。</param>
    /// <param name="timeProvider">用于令牌期限判断的时钟。</param>
    /// <param name="clientVersion">测试或特殊宿主可显式提供的前端产品版本。</param>
    public UserSessionService(
        IHttpClientFactory httpClientFactory,
        ILogger<UserSessionService> logger,
        IAuthenticationSessionClient authentication,
        TimeProvider? timeProvider = null,
        string? clientVersion = null)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        client = httpClientFactory.CreateClient("Anonymous");
        this.authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
        this.timeProvider = timeProvider ?? TimeProvider.System;
        ClientVersion = NormalizeOptional(clientVersion);
        if (ClientVersion.Length == 0)
        {
            ClientVersion = ResolveClientVersion();
        }
    }

    /// <summary>在会话可见状态变化时通知 Razor 页面。</summary>
    public event Action? StateChanged;

    /// <summary>获取当前会话的用户资料；短期令牌过期不会直接清除会话。</summary>
    public UserInfo? UserInfo => Volatile.Read(ref userInfo);

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

    /// <summary>获取服务端当前发布的用户许可协议。</summary>
    public string UserAgreement { get; private set; } = string.Empty;

    /// <summary>获取服务端当前发布的隐私条款。</summary>
    public string PrivacyPolicy { get; private set; } = string.Empty;

    /// <summary>获取公共系统信息是否已经完成首次加载。</summary>
    public bool IsSystemInfoLoaded { get; private set; }

    /// <summary>获取或设置当前工作台是否检测到触摸输入。</summary>
    public bool IsTouchDetected { get; set; }

    /// <summary>获取当前宿主是否支持安全保存登录信息。</summary>
    public bool CanRememberLogin => authentication.CanRememberLogin;

    /// <summary>获取是否正在尝试使用宿主保存的信息自动登录。</summary>
    public bool IsAutomaticSignInInProgress { get; private set; }

    /// <summary>获取自动登录失败后可向用户解释的消息。</summary>
    public string? AutomaticSignInError { get; private set; }

    /// <summary>获取保存或清除登录信息失败时的警告。</summary>
    public string? CredentialPersistenceWarning { get; private set; }

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
            var userAgreementTask = TryGetNoticeAsync("SystemInfo/UserAgreement/", cancellationToken);
            var privacyPolicyTask = TryGetNoticeAsync("SystemInfo/PrivacyPolicy/", cancellationToken);

            await Task.WhenAll(
                publicInfoTask,
                coverLetterTask,
                noticeTask,
                userAgreementTask,
                privacyPolicyTask);

            var publicInfo = await publicInfoTask;
            CaseNumber = NormalizeOptional(publicInfo.CaseNumber);
            ServerVersion = NormalizeOptional(publicInfo.ServerVersion);
            CoverLetter = await coverLetterTask;
            Notice = await noticeTask;
            UserAgreement = await userAgreementTask;
            PrivacyPolicy = await privacyPolicyTask;
        }
        finally
        {
            IsSystemInfoLoaded = true;
            StateChanged?.Invoke();
        }
    }

    /// <summary>同步读取仍有效的访问令牌；需要续期的请求应使用异步方法。</summary>
    public bool TryGetAccessToken(out string token)
    {
        lock (stateLock)
        {
            token = tokens is not null && tokens.AccessTokenExpiresAtUtc > timeProvider.GetUtcNow()
                ? tokens.AccessToken
                : string.Empty;
            return token.Length > 0;
        }
    }

    /// <summary>取得有效访问令牌；并发请求通过同一闸门共享刷新结果。</summary>
    /// <param name="cancellationToken">仅取消本次等待，不中断其他请求共用的刷新。</param>
    /// <param name="rejectedToken">服务器明确报告已过期的旧令牌，用于避免重复刷新。</param>
    public Task<string?> GetValidAccessTokenAsync(
        CancellationToken cancellationToken = default, string? rejectedToken = null) =>
        GetValidAccessTokenCoreAsync(rejectedToken).WaitAsync(cancellationToken);

    /// <summary>在需要登录的页面操作开始前确认会话，并按需完成续期。</summary>
    public async Task<bool> EnsureAuthenticatedAsync(CancellationToken cancellationToken = default) =>
        await GetValidAccessTokenAsync(cancellationToken) is not null;

    /// <inheritdoc />
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        await InitializeAsync();
        return CreateAuthenticationState();
    }

    /// <summary>使用账号密码登录，不持久化恢复凭据。</summary>
    public Task SignInAsync(
        string userName, string password, CancellationToken cancellationToken = default) =>
        SignInAsync(userName, password, rememberLogin: false, cancellationToken);

    /// <summary>使用账号密码登录，并将保持登录的选择交给宿主执行。</summary>
    public async Task SignInAsync(
        string userName, string password, bool rememberLogin, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(password))
        {
            throw new InvalidOperationException("请输入用户名和密码。");
        }

        var revision = ClearSession();
        AutomaticSignInError = null;
        CredentialPersistenceWarning = null;
        await operationGate.WaitAsync(cancellationToken);
        try
        {
            var result = await authentication.SignInAsync(new LoginRequestDto
            {
                UserName = userName.Trim(),
                Password = password,
                RememberLogin = rememberLogin
            }, cancellationToken);
            ApplySession(result, revision);
        }
        finally
        {
            operationGate.Release();
        }
    }

    /// <summary>立即结束本地登录，再撤销宿主当前凭据；后续旧响应不能恢复该会话。</summary>
    public Task SignOutAsync(CancellationToken cancellationToken = default) =>
        SignOutCoreAsync(rejectedToken: null, cancellationToken);

    /// <summary>仅当拒绝的令牌仍属于当前会话时退出，保护已轮换或重新登录的新状态。</summary>
    public Task RejectAccessTokenAsync(string rejectedToken, CancellationToken cancellationToken = default) =>
        SignOutCoreAsync(rejectedToken, cancellationToken);

    /// <summary>响应其他浏览器标签页的登录变更，重新恢复共享 Cookie 所属会话。</summary>
    public async Task ReloadExternalSessionAsync()
    {
        var revision = ClearSession();
        await RestoreSessionAsync(revision);
    }

    private async Task<string?> GetValidAccessTokenCoreAsync(string? rejectedToken)
    {
        await InitializeAsync();
        long revision;
        lock (stateLock)
        {
            revision = generation;
        }

        await operationGate.WaitAsync();
        try
        {
            AuthenticationTokensDto? current;
            lock (stateLock)
            {
                if (revision != generation)
                {
                    throw SessionChanged();
                }
                current = tokens;
            }
            if (current is null)
            {
                return null;
            }
            if (rejectedToken is not null && new UserInfo(rejectedToken).SessionId != current.SessionId)
            {
                // 延迟返回的旧请求不能以新登录账号的身份重发。
                throw SessionChanged();
            }

            var now = timeProvider.GetUtcNow();
            if (current.SessionExpiresAtUtc <= now || current.RefreshExpiresAtUtc <= now)
            {
                ClearSessionIfCurrent(revision);
                throw new SessionAuthenticationException(
                    AuthenticationErrorCodes.SessionInvalid, "登录会话已过期，请重新登录。");
            }
            if (rejectedToken is not null && current.AccessToken != rejectedToken &&
                current.AccessTokenExpiresAtUtc > now)
            {
                return current.AccessToken;
            }
            if (rejectedToken is null && current.AccessTokenExpiresAtUtc > now.AddMinutes(1))
            {
                return current.AccessToken;
            }
            if (retryRefreshAfter > now)
            {
                if (rejectedToken is null && current.AccessTokenExpiresAtUtc > now)
                {
                    return current.AccessToken;
                }
                throw new InvalidOperationException(refreshFailure ?? "暂时无法刷新登录，请稍后重试。");
            }

            try
            {
                var result = await authentication.RefreshAsync(current.SessionId);
                if (result.SessionId != current.SessionId || !ApplySession(result, revision))
                {
                    throw SessionChanged();
                }
                return result.AccessToken;
            }
            catch (SessionAuthenticationException)
            {
                ClearSessionIfCurrent(revision);
                throw;
            }
            catch (Exception exception) when (
                exception is InvalidOperationException or HttpRequestException or IOException or UnauthorizedAccessException or OperationCanceledException)
            {
                lock (stateLock)
                {
                    if (revision != generation)
                    {
                        throw SessionChanged();
                    }
                }
                retryRefreshAfter = timeProvider.GetUtcNow().AddSeconds(5);
                refreshFailure = exception.Message;
                // 提前刷新失败时，仍有效的短令牌可以继续使用；过期后必须等待恢复。
                if (rejectedToken is null && current.AccessTokenExpiresAtUtc > timeProvider.GetUtcNow())
                {
                    return current.AccessToken;
                }
                throw new InvalidOperationException("暂时无法刷新登录，请检查网络后重试。", exception);
            }
        }
        finally
        {
            operationGate.Release();
        }
    }

    private async Task SignOutCoreAsync(string? rejectedToken, CancellationToken cancellationToken)
    {
        Guid? sessionId;
        lock (stateLock)
        {
            if (rejectedToken is not null && tokens?.AccessToken != rejectedToken)
            {
                return;
            }
            sessionId = tokens?.SessionId;
            generation++;
            tokens = null;
            userInfo = null;
        }
        AutomaticSignInError = null;
        CredentialPersistenceWarning = null;
        NotifySessionChanged();
        // 退出意图已经生效，等待正在进行的轮换结束后再清除其最新凭据。
        await operationGate.WaitAsync(CancellationToken.None);
        try
        {
            await authentication.SignOutAsync(sessionId, cancellationToken);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or HttpRequestException or IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            logger.LogWarning(exception, "未能完整撤销或清除当前登录会话。");
            CredentialPersistenceWarning = "已退出当前界面，但注销未完整完成；请联网后重试。";
        }
        finally
        {
            operationGate.Release();
            NotifySessionChanged();
        }
    }

    private async Task InitializeCoreAsync()
    {
        long revision;
        lock (stateLock)
        {
            revision = generation;
        }
        await Task.WhenAll(GetSystemInfoAsync(), RestoreSessionAsync(revision));
    }

    private async Task RestoreSessionAsync(long revision)
    {
        IsAutomaticSignInInProgress = true;
        StateChanged?.Invoke();
        await operationGate.WaitAsync();
        try
        {
            lock (stateLock)
            {
                if (revision != generation || tokens is not null)
                {
                    return;
                }
            }

            var result = await authentication.RestoreAsync();
            if (result is not null)
            {
                ApplySession(result, revision);
            }
        }
        catch (SessionAuthenticationException exception)
        {
            if (ClearSessionIfCurrent(revision))
            {
                AutomaticSignInError = exception.Message;
            }
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or HttpRequestException or IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            logger.LogWarning(exception, "暂时无法恢复保存的登录会话。");
            lock (stateLock)
            {
                if (revision == generation)
                {
                    AutomaticSignInError = "暂时无法恢复登录，请检查网络及本机存储后重新加载或手动登录。";
                }
            }
        }
        finally
        {
            operationGate.Release();
            IsAutomaticSignInInProgress = false;
            StateChanged?.Invoke();
        }
    }

    private bool ApplySession(AuthenticationTokensDto result, long revision)
    {
        var now = timeProvider.GetUtcNow();
        UserInfo user;
        try
        {
            user = new UserInfo(result.AccessToken);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException("服务器返回了无效的访问令牌。", exception);
        }
        if (result.SessionId == Guid.Empty || user.SessionId != result.SessionId ||
            result.AccessTokenExpiresAtUtc <= now || user.ExpiresAt != result.AccessTokenExpiresAtUtc ||
            result.RefreshExpiresAtUtc <= now || result.SessionExpiresAtUtc < result.RefreshExpiresAtUtc)
        {
            throw new InvalidOperationException("服务器返回了无效的登录会话。");
        }

        lock (stateLock)
        {
            if (revision != generation)
            {
                return false;
            }
            tokens = result with { RefreshToken = null };
            userInfo = user;
            retryRefreshAfter = default;
            refreshFailure = null;
        }
        AutomaticSignInError = null;
        NotifySessionChanged();
        return true;
    }

    private long ClearSession()
    {
        long revision;
        lock (stateLock)
        {
            revision = ++generation;
            tokens = null;
            userInfo = null;
            retryRefreshAfter = default;
            refreshFailure = null;
        }
        NotifySessionChanged();
        return revision;
    }

    private bool ClearSessionIfCurrent(long revision)
    {
        lock (stateLock)
        {
            if (generation != revision)
            {
                return false;
            }
            generation++;
            tokens = null;
            userInfo = null;
        }
        NotifySessionChanged();
        return true;
    }

    private AuthenticationState CreateAuthenticationState()
    {
        lock (stateLock)
        {
            var now = timeProvider.GetUtcNow();
            var user = userInfo is not null && tokens is not null &&
                tokens.RefreshExpiresAtUtc > now && tokens.SessionExpiresAtUtc > now
                ? new ClaimsPrincipal(new ClaimsIdentity(
                    userInfo.Claims, "jwt", ClaimTypes.Name, ClaimTypes.Role))
                : new ClaimsPrincipal(new ClaimsIdentity());
            return new AuthenticationState(user);
        }
    }

    private void NotifySessionChanged()
    {
        NotifyAuthenticationStateChanged(Task.FromResult(CreateAuthenticationState()));
        StateChanged?.Invoke();
    }

    private static SessionAuthenticationException SessionChanged() => new(
        AuthenticationErrorCodes.SessionChanged, "登录状态已改变，请重新执行操作。");

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
}
