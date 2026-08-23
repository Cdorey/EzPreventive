using EzNutrition.Presentation.Models;
using EzNutrition.Shared.Data.Entities;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Json;
using System.Security.Claims;

namespace EzNutrition.Presentation.Services
{
    public sealed class UserSessionService(
        IHttpClientFactory httpClientFactory,
        ILogger<UserSessionService> logger) : AuthenticationStateProvider
    {
        private static readonly TimeSpan LoginRetryDelay = TimeSpan.FromMilliseconds(250);

        private readonly HttpClient client = httpClientFactory.CreateClient("Anonymous");
        private UserInfo? userInfo;

        public event Action? StateChanged;

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

        public string CaseNumber { get; private set; } = string.Empty;

        public string CoverLetter { get; private set; } = string.Empty;

        public string Notice { get; private set; } = string.Empty;

        public bool IsSystemInfoLoaded { get; private set; }

        public bool IsTouchDetected { get; set; }

        public async Task GetSystemInfoAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var caseNumberTask = TryGetStringAsync("SystemInfo/CaseNumber/", cancellationToken);
                var coverLetterTask = TryGetNoticeAsync("SystemInfo/CoverLetter/", cancellationToken);
                var noticeTask = TryGetNoticeAsync("SystemInfo/Notice/", cancellationToken);

                await Task.WhenAll(caseNumberTask, coverLetterTask, noticeTask);

                CaseNumber = await caseNumberTask;
                CoverLetter = await coverLetterTask;
                Notice = await noticeTask;
            }
            finally
            {
                IsSystemInfoLoaded = true;
                StateChanged?.Invoke();
            }
        }

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

        public async Task SignInAsync(
            string userName,
            string password,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(password))
            {
                throw new InvalidOperationException("请输入用户名和密码。");
            }

            using var response = await PostLoginAsync(userName.Trim(), password, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    response.StatusCode == System.Net.HttpStatusCode.Unauthorized
                        ? "用户名或密码错误、邮箱尚未确认，或账户暂时被锁定。"
                        : "登录失败，请稍后重试。");
            }

            var token = await response.Content.ReadAsStringAsync(cancellationToken);
            try
            {
                var signedInUser = new UserInfo(token);
                if (signedInUser.IsExpired)
                {
                    throw new InvalidOperationException("服务器返回的登录凭据已经过期。");
                }

                UserInfo = signedInUser;
            }
            catch (ArgumentException ex)
            {
                logger.LogError(ex, "The login endpoint returned an invalid JWT.");
                throw new InvalidOperationException("服务器返回了无效的登录凭据，请稍后重试。", ex);
            }
        }

        public Task SignOutAsync()
        {
            UserInfo = null;
            return Task.CompletedTask;
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
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "Unable to reach the login endpoint.");
                throw new InvalidOperationException("无法连接服务器，请检查网络后重试。", ex);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "The login request timed out.");
                throw new InvalidOperationException("登录请求超时，请稍后重试。", ex);
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

        private static bool IsTransientServerFailure(System.Net.HttpStatusCode statusCode) =>
            (int)statusCode is 500 or 502 or 503 or 504;

        private async Task<string> TryGetStringAsync(string requestUri, CancellationToken cancellationToken)
        {
            try
            {
                return await client.GetStringAsync(requestUri, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or NotSupportedException)
            {
                logger.LogWarning(ex, "Unable to load system information from {RequestUri}.", requestUri);
                return string.Empty;
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Loading system information from {RequestUri} timed out.", requestUri);
                return string.Empty;
            }
        }

        private async Task<string> TryGetNoticeAsync(string requestUri, CancellationToken cancellationToken)
        {
            try
            {
                var notice = await client.GetFromJsonAsync<Notice>(requestUri, cancellationToken);
                return notice?.Description ?? string.Empty;
            }
            catch (Exception ex) when (ex is HttpRequestException or NotSupportedException or System.Text.Json.JsonException)
            {
                logger.LogWarning(ex, "Unable to load notice from {RequestUri}.", requestUri);
                return string.Empty;
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Loading notice from {RequestUri} timed out.", requestUri);
                return string.Empty;
            }
        }
    }
}
