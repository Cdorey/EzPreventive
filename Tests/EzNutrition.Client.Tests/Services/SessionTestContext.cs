using EzNutrition.Presentation.Services;
using EzNutrition.Shared.Data.DTO;
using Microsoft.Extensions.Logging.Abstractions;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;

namespace EzNutrition.Client.Tests.Services;

/// <summary>用受控时钟和宿主响应验证共享会话，避免依赖真实等待或外部网络。</summary>
internal sealed class SessionTestContext : IDisposable
{
    internal static readonly Uri BaseAddress = new("https://app.example.test/");
    private readonly HttpClient client;

    internal SessionTestContext()
    {
        client = new HttpClient(new PublicInfoHandler()) { BaseAddress = BaseAddress };
        Session = new UserSessionService(new ClientFactory(client),
            NullLogger<UserSessionService>.Instance, Authentication, Clock, clientVersion: "2.2.0.0");
    }

    internal TestClock Clock { get; } = new();
    internal TestAuthenticationClient Authentication { get; } = new();
    internal UserSessionService Session { get; }

    internal async Task<AuthenticationTokensDto> SignInAsync(
        string userName = "test-user", TimeSpan? lifetime = null, Guid? sessionId = null)
    {
        var tokens = CreateTokens(Clock.GetUtcNow(), userName, lifetime, sessionId);
        Authentication.SignIn = _ => Task.FromResult(tokens);
        Authentication.Refresh = id => Task.FromResult(CreateTokens(Clock.GetUtcNow(), userName, sessionId: id));
        await Session.SignInAsync(userName, "password");
        return tokens;
    }

    internal static AuthenticationTokensDto CreateTokens(
        DateTimeOffset now, string userName = "test-user", TimeSpan? lifetime = null, Guid? sessionId = null,
        IEnumerable<Claim>? additionalClaims = null)
    {
        now = DateTimeOffset.FromUnixTimeSeconds(now.ToUnixTimeSeconds());
        var expires = now + (lifetime ?? TimeSpan.FromMinutes(15));
        var id = sessionId ?? Guid.NewGuid();
        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, userName + "-id"),
            new(JwtRegisteredClaimNames.UniqueName, userName),
            new("sid", id.ToString("D")),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        ];
        claims.AddRange(additionalClaims ?? []);
        var jwt = new JwtSecurityToken(claims: claims, expires: expires.UtcDateTime);
        return new AuthenticationTokensDto
        {
            SessionId = id,
            AccessToken = new JwtSecurityTokenHandler().WriteToken(jwt),
            AccessTokenExpiresAtUtc = expires,
            RefreshExpiresAtUtc = now.AddDays(7),
            SessionExpiresAtUtc = now.AddDays(30),
            RefreshToken = "host-only-refresh-token",
            RememberLogin = true
        };
    }

    public void Dispose() => client.Dispose();

    internal sealed class TestClock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        public override DateTimeOffset GetUtcNow() => now;
        internal void Advance(TimeSpan duration) => now += duration;
    }

    private sealed class ClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class PublicInfoHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = request.RequestUri!.AbsolutePath.Contains("PublicInfo", StringComparison.Ordinal)
                    ? JsonContent.Create(new { caseNumber = "test-case", serverVersion = "2.2.0.0" })
                    : JsonContent.Create(new { description = "test-notice" })
            });
    }
}

/// <summary>模拟宿主认证边界，记录调用次数并允许阻塞指定步骤以复现竞争。</summary>
internal sealed class TestAuthenticationClient : IAuthenticationSessionClient
{
    public bool CanRememberLogin => true;
    internal Func<LoginRequestDto, Task<AuthenticationTokensDto>> SignIn { get; set; } =
        _ => throw new InvalidOperationException("测试未安排登录响应。");
    internal Func<Guid, Task<AuthenticationTokensDto>> Refresh { get; set; } =
        _ => throw new InvalidOperationException("测试未安排刷新响应。");
    internal Func<Task<AuthenticationTokensDto?>> Restore { get; set; } = () => Task.FromResult<AuthenticationTokensDto?>(null);
    internal Func<Guid?, Task> SignOut { get; set; } = _ => Task.CompletedTask;
    internal int RefreshCount { get; private set; }
    internal int RestoreCount { get; private set; }
    internal int SignOutCount { get; private set; }

    public Task<AuthenticationTokensDto> SignInAsync(LoginRequestDto request, CancellationToken cancellationToken = default) => SignIn(request);
    public Task<AuthenticationTokensDto> RefreshAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        RefreshCount++;
        return Refresh(sessionId);
    }
    public Task<AuthenticationTokensDto?> RestoreAsync(CancellationToken cancellationToken = default)
    {
        RestoreCount++;
        return Restore();
    }
    public Task SignOutAsync(Guid? sessionId, CancellationToken cancellationToken = default)
    {
        SignOutCount++;
        return SignOut(sessionId);
    }
}
