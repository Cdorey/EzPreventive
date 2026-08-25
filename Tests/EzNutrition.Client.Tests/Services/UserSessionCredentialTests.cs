using EzNutrition.Presentation.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;

namespace EzNutrition.Client.Tests.Services;

/// <summary>
/// 验证用户会话与宿主登录信息存储之间的安全语义。
/// </summary>
public sealed class UserSessionCredentialTests
{
    private static readonly Uri ServerBaseAddress = new("https://app.example.test/");

    [Fact]
    public async Task Remembered_sign_in_saves_the_normalized_credential()
    {
        var credentialStore = new RecordingCredentialStore();
        var handler = new SessionEndpointHandler(CreateToken("remembered-user"));
        var session = CreateSession(handler, credentialStore);

        await session.SignInAsync("  remembered-user  ", "test-password", rememberLogin: true);

        Assert.NotNull(credentialStore.SavedCredential);
        Assert.Equal("remembered-user", credentialStore.SavedCredential.UserName);
        Assert.Equal("test-password", credentialStore.SavedCredential.Password);
        Assert.True(session.TryGetAccessToken(out _));
    }

    [Fact]
    public async Task Sign_in_without_remembering_removes_an_existing_credential()
    {
        var credentialStore = new RecordingCredentialStore(
            new SavedLoginCredential("old-user", "old-password"));
        var handler = new SessionEndpointHandler(CreateToken("current-user"));
        var session = CreateSession(handler, credentialStore);

        await session.SignInAsync("current-user", "current-password", rememberLogin: false);

        Assert.Null(credentialStore.SavedCredential);
        Assert.Equal(1, credentialStore.ClearCount);
    }

    [Fact]
    public async Task Explicit_sign_out_clears_the_saved_credential()
    {
        var credentialStore = new RecordingCredentialStore();
        var handler = new SessionEndpointHandler(CreateToken("current-user"));
        var session = CreateSession(handler, credentialStore);
        await session.SignInAsync("current-user", "current-password", rememberLogin: true);

        await session.SignOutAsync();

        Assert.Null(credentialStore.SavedCredential);
        Assert.Equal(1, credentialStore.ClearCount);
        Assert.False(session.TryGetAccessToken(out _));
    }

    [Fact]
    public async Task Token_rejection_sign_out_preserves_the_saved_credential()
    {
        var credentialStore = new RecordingCredentialStore();
        var handler = new SessionEndpointHandler(CreateToken("current-user"));
        var session = CreateSession(handler, credentialStore);
        await session.SignInAsync("current-user", "current-password", rememberLogin: true);

        await session.SignOutAsync(forgetSavedLogin: false);

        Assert.NotNull(credentialStore.SavedCredential);
        Assert.Equal(0, credentialStore.ClearCount);
        Assert.False(session.TryGetAccessToken(out _));
    }

    [Fact]
    public async Task Initialization_uses_a_saved_credential_only_once()
    {
        var credentialStore = new RecordingCredentialStore(
            new SavedLoginCredential("saved-user", "saved-password"));
        var handler = new SessionEndpointHandler(CreateToken("saved-user"));
        var session = CreateSession(handler, credentialStore);

        await Task.WhenAll(session.InitializeAsync(), session.InitializeAsync());

        Assert.True(session.TryGetAccessToken(out _));
        Assert.Equal(1, credentialStore.ReadCount);
        Assert.Equal(1, handler.LoginCount);
        Assert.Equal(3, handler.SystemInfoRequestCount);
        Assert.Null(session.AutomaticSignInError);
    }

    [Fact]
    public async Task Rejected_saved_credential_is_removed()
    {
        var credentialStore = new RecordingCredentialStore(
            new SavedLoginCredential("rejected-user", "rejected-password"));
        var handler = new SessionEndpointHandler(loginStatusCode: HttpStatusCode.Unauthorized);
        var session = CreateSession(handler, credentialStore);

        await session.InitializeAsync();

        Assert.Null(credentialStore.SavedCredential);
        Assert.Equal(1, credentialStore.ClearCount);
        Assert.Contains("本机副本已经清除", session.AutomaticSignInError, StringComparison.Ordinal);
        Assert.False(session.TryGetAccessToken(out _));
    }

    [Fact]
    public async Task Temporary_auto_sign_in_failure_preserves_the_saved_credential()
    {
        var savedCredential = new SavedLoginCredential("offline-user", "offline-password");
        var credentialStore = new RecordingCredentialStore(savedCredential);
        var handler = new SessionEndpointHandler(throwOnLogin: true);
        var session = CreateSession(handler, credentialStore);

        await session.InitializeAsync();

        Assert.Same(savedCredential, credentialStore.SavedCredential);
        Assert.Equal(0, credentialStore.ClearCount);
        Assert.Equal("offline-user", session.SuggestedUserName);
        Assert.Contains("已保留本机登录信息", session.AutomaticSignInError, StringComparison.Ordinal);
    }

    private static UserSessionService CreateSession(
        HttpMessageHandler handler,
        ILoginCredentialStore credentialStore)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = ServerBaseAddress
        };
        return new UserSessionService(
            new StaticHttpClientFactory(client),
            NullLogger<UserSessionService>.Instance,
            credentialStore);
    }

    private static string CreateToken(string userName)
    {
        var token = new JwtSecurityToken(
            claims: [new Claim(JwtRegisteredClaimNames.UniqueName, userName)],
            expires: DateTime.UtcNow.AddHours(1));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class StaticHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class RecordingCredentialStore : ILoginCredentialStore
    {
        internal RecordingCredentialStore(SavedLoginCredential? initialCredential = null)
        {
            SavedCredential = initialCredential;
        }

        public bool IsAvailable => true;

        internal int ClearCount { get; private set; }

        internal int ReadCount { get; private set; }

        internal SavedLoginCredential? SavedCredential { get; private set; }

        public ValueTask<SavedLoginCredential?> ReadAsync(CancellationToken cancellationToken = default)
        {
            ReadCount++;
            return ValueTask.FromResult(SavedCredential);
        }

        public ValueTask SaveAsync(
            SavedLoginCredential credential,
            CancellationToken cancellationToken = default)
        {
            SavedCredential = credential;
            return ValueTask.CompletedTask;
        }

        public ValueTask ClearAsync(CancellationToken cancellationToken = default)
        {
            ClearCount++;
            SavedCredential = null;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class SessionEndpointHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode loginStatusCode;
        private readonly string? loginToken;
        private readonly bool throwOnLogin;
        private int loginCount;
        private int systemInfoRequestCount;

        internal SessionEndpointHandler(
            string? loginToken = null,
            HttpStatusCode loginStatusCode = HttpStatusCode.OK,
            bool throwOnLogin = false)
        {
            this.loginToken = loginToken;
            this.loginStatusCode = loginStatusCode;
            this.throwOnLogin = throwOnLogin;
        }

        internal int LoginCount => Volatile.Read(ref loginCount);

        internal int SystemInfoRequestCount => Volatile.Read(ref systemInfoRequestCount);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var relativePath = ServerBaseAddress.MakeRelativeUri(
                request.RequestUri ?? throw new InvalidOperationException("请求缺少 URI。")).ToString();
            if (string.Equals(relativePath, "Auth/Login", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref loginCount);
                if (throwOnLogin)
                {
                    throw new HttpRequestException("Simulated network failure.");
                }

                return Task.FromResult(new HttpResponseMessage(loginStatusCode)
                {
                    Content = new StringContent(loginToken ?? string.Empty)
                });
            }

            Interlocked.Increment(ref systemInfoRequestCount);
            var content = relativePath switch
            {
                "SystemInfo/CaseNumber/" => "test-case-number",
                "SystemInfo/CoverLetter/" => """{"description":"test-cover-letter"}""",
                "SystemInfo/Notice/" => """{"description":"test-notice"}""",
                _ => throw new InvalidOperationException($"Unexpected request path: {relativePath}")
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content)
            });
        }
    }
}
