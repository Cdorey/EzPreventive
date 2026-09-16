using EzNutrition.Presentation.Services;
using EzNutrition.Shared.Data.DTO;
using EzNutrition.Wpf.Configuration;
using EzNutrition.Wpf.Security;
using EzNutrition.Wpf.Tests.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;

namespace EzNutrition.Wpf.Tests.Security;

/// <summary>使用真实 DPAPI 文件与受控认证端点验证桌面会话恢复及跨进程协调。</summary>
public sealed class WpfAuthenticationSessionClientTests
{
    [Fact]
    public async Task A_new_process_restores_with_the_saved_refresh_credential_and_never_resends_a_password()
    {
        using var context = new NativeContext();
        var first = context.CreateClient();
        var original = await first.SignInAsync(Login(remember: true));
        Assert.Null(original.RefreshToken);
        var saved = await context.Store.ReadAsync();
        Assert.NotNull(saved);
        var restored = await context.CreateClient().RestoreAsync();
        Assert.NotNull(restored);
        Assert.Equal(original.SessionId, restored.SessionId);
        Assert.NotEqual(original.AccessToken, restored.AccessToken);
        Assert.NotEqual(saved.RefreshToken, (await context.Store.ReadAsync())!.RefreshToken);
        Assert.Equal(1, context.Server.LoginCount);
        Assert.Equal(1, context.Server.RefreshCount);
    }

    [Fact]
    public async Task Memory_only_login_can_refresh_without_persisting_or_clearing_another_process_login()
    {
        using var context = new NativeContext();
        var transient = context.CreateClient();
        var first = await transient.SignInAsync(Login(remember: false));
        Assert.False(context.Store.HasSavedCredential);
        Assert.Null(await context.CreateClient().RestoreAsync());
        await transient.RefreshAsync(first.SessionId);
        Assert.False(context.Store.HasSavedCredential);

        var remembered = await context.CreateClient().SignInAsync(Login(remember: true));
        await transient.RefreshAsync(first.SessionId);
        Assert.Equal(remembered.SessionId, (await context.Store.ReadAsync())!.SessionId);
        await transient.SignOutAsync(first.SessionId);
        Assert.Equal(remembered.SessionId, (await context.Store.ReadAsync())!.SessionId);
    }

    [Fact]
    public async Task Concurrent_processes_reload_the_rotated_credential_under_the_same_lock()
    {
        using var context = new NativeContext();
        var first = context.CreateClient();
        var original = await first.SignInAsync(Login(remember: true));
        var second = context.CreateClient();
        await second.RestoreAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        context.Server.BeforeRefresh = () =>
        {
            entered.TrySetResult();
            return released.Task;
        };
        var firstRefresh = first.RefreshAsync(original.SessionId);
        await entered.Task;
        var secondRefresh = second.RefreshAsync(original.SessionId);
        Assert.False(secondRefresh.IsCompleted);
        released.SetResult();
        var responses = await Task.WhenAll(firstRefresh, secondRefresh);
        Assert.All(responses, response => Assert.Equal(original.SessionId, response.SessionId));
        Assert.Equal(3, context.Server.RefreshCount);
        Assert.Equal(0, context.Server.RejectedRefreshCount);
    }

    [Fact]
    public async Task An_old_process_cannot_consume_or_erase_another_accounts_saved_credential()
    {
        using var context = new NativeContext();
        var oldProcess = context.CreateClient();
        var old = await oldProcess.SignInAsync(Login(remember: true));
        var current = await context.CreateClient().SignInAsync(Login(remember: true));
        var error = await Assert.ThrowsAsync<SessionAuthenticationException>(() => oldProcess.RefreshAsync(old.SessionId));
        Assert.Equal(AuthenticationErrorCodes.SessionChanged, error.Code);
        await oldProcess.SignOutAsync(old.SessionId);
        Assert.Equal(current.SessionId, (await context.Store.ReadAsync())!.SessionId);
        Assert.Equal(0, context.Server.RefreshCount);
    }

    [Fact]
    public async Task Network_failure_preserves_refresh_but_explicit_logout_removes_the_local_copy()
    {
        using var context = new NativeContext();
        var client = context.CreateClient();
        var login = await client.SignInAsync(Login(remember: true));
        var saved = await context.Store.ReadAsync();
        context.Server.Offline = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.RefreshAsync(login.SessionId));
        Assert.Equal(saved, await context.Store.ReadAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SignOutAsync(login.SessionId));
        Assert.False(context.Store.HasSavedCredential);
        Assert.Null(await context.CreateClient().RestoreAsync());
    }

    [Fact]
    public async Task A_rejected_refresh_removes_only_its_saved_session()
    {
        using var context = new NativeContext();
        var client = context.CreateClient();
        var login = await client.SignInAsync(Login(remember: true));
        context.Server.Revoked.Add(login.SessionId);
        await Assert.ThrowsAsync<SessionAuthenticationException>(() => client.RefreshAsync(login.SessionId));
        Assert.False(context.Store.HasSavedCredential);
    }

    private static LoginRequestDto Login(bool remember) => new()
    {
        UserName = "desktop-user", Password = "only-sent-at-login", RememberLogin = remember
    };

    private sealed class NativeContext : IDisposable
    {
        private readonly TempDirectory directory = new();
        private readonly HttpClient http;
        private readonly WpfHostSettings settings;
        internal NativeContext()
        {
            settings = WpfUserSettingsStoreTests.CreateSettings(
                "https://server.example.test/", ServerTransportSecurity.StrictHttps, directory.RootPath);
            http = new HttpClient(Server) { BaseAddress = settings.ServerBaseAddress };
            Store = CreateStore();
        }
        internal AuthenticationEndpoint Server { get; } = new();
        internal DpapiLoginCredentialStore Store { get; }
        internal WpfAuthenticationSessionClient CreateClient() =>
            new(new ClientFactory(http), CreateStore(), NullLogger<WpfAuthenticationSessionClient>.Instance);
        private DpapiLoginCredentialStore CreateStore() => new(WpfUserDataPaths.Create(directory.RootPath), settings);
        public void Dispose()
        {
            http.Dispose();
            directory.Dispose();
        }
    }

    private sealed class ClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            Assert.Equal("Authentication", name);
            return client;
        }
    }

    /// <summary>只接受每个会话最新的一次性凭据，使缺少文件锁或未重新读盘的问题可被观察。</summary>
    private sealed class AuthenticationEndpoint : HttpMessageHandler
    {
        private readonly Dictionary<Guid, AuthenticationTokensDto> sessions = [];
        internal int LoginCount { get; private set; }
        internal int RefreshCount { get; private set; }
        internal int RejectedRefreshCount { get; private set; }
        internal HashSet<Guid> Revoked { get; } = [];
        internal bool Offline { get; set; }
        internal Func<Task>? BeforeRefresh { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Offline) throw new HttpRequestException("simulated offline");
            Assert.Null(request.Headers.Authorization);
            Assert.Equal(HttpMethod.Post, request.Method);
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/Auth/Login")
            {
                LoginCount++;
                var login = await request.Content!.ReadFromJsonAsync<LoginRequestDto>(cancellationToken);
                var tokens = CreateTokens(Guid.NewGuid(), login!.RememberLogin);
                sessions[tokens.SessionId] = tokens;
                return Json(tokens);
            }
            var credential = await request.Content!.ReadFromJsonAsync<RefreshRequestDto>(cancellationToken);
            Assert.NotNull(credential?.SessionId);
            if (path == "/Auth/Logout")
            {
                Revoked.Add(credential.SessionId.Value);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            Assert.Equal("/Auth/Refresh", path);
            RefreshCount++;
            if (BeforeRefresh is not null) await BeforeRefresh().WaitAsync(cancellationToken);
            var previous = sessions[credential.SessionId.Value];
            if (Revoked.Contains(previous.SessionId) || previous.RefreshToken != credential.RefreshToken)
            {
                RejectedRefreshCount++;
                return new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = JsonContent.Create(new AuthenticationErrorDto(AuthenticationErrorCodes.SessionInvalid, "invalid"))
                };
            }
            var next = CreateTokens(previous.SessionId, previous.RememberLogin);
            sessions[next.SessionId] = next;
            return Json(next);
        }

        private static HttpResponseMessage Json(AuthenticationTokensDto tokens) => new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(tokens)
        };

        private static AuthenticationTokensDto CreateTokens(Guid sessionId, bool remember)
        {
            var now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            var jwt = new JwtSecurityToken(claims:
            [
                new Claim("sid", sessionId.ToString("D")),
                new Claim("sub", "desktop-user-id"),
                new Claim("unique_name", "desktop-user"),
                new Claim("jti", Guid.NewGuid().ToString("N"))
            ], expires: now.AddMinutes(15).UtcDateTime);
            return new AuthenticationTokensDto
            {
                SessionId = sessionId,
                AccessToken = new JwtSecurityTokenHandler().WriteToken(jwt),
                AccessTokenExpiresAtUtc = now.AddMinutes(15),
                RefreshToken = Guid.NewGuid().ToString("N"),
                RefreshExpiresAtUtc = now.AddDays(7),
                SessionExpiresAtUtc = now.AddDays(30),
                RememberLogin = remember
            };
        }
    }
}
