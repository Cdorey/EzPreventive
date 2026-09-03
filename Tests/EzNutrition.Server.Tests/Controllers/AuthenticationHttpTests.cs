using EzNutrition.Server.Controllers;
using EzNutrition.Server.Data;
using EzNutrition.Server.Data.Repositories;
using EzNutrition.Server.Extension;
using EzNutrition.Server.Services;
using EzNutrition.Server.Services.Settings;
using EzNutrition.Shared.Data.DTO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.RateLimiting;

namespace EzNutrition.Server.Tests.Controllers;

/// <summary>经过真实 MVC、防伪和 JWT 中间件验证两种宿主协议，而不连接生产数据库。</summary>
public sealed class AuthenticationHttpTests
{
    [Fact]
    public async Task Native_login_rotation_and_logout_revoke_access_immediately()
    {
        await using var host = await HttpHost.CreateAsync();
        using var login = await host.Client.PostAsJsonAsync("Auth/Login", Credentials());
        var first = await ReadTokens(login);
        Assert.False(login.Headers.Contains("Set-Cookie"));
        Assert.NotNull(first.RefreshToken);
        Assert.True(login.Headers.CacheControl?.NoStore);
        using var allowed = await host.ProtectedAsync(first.AccessToken);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);

        using var refresh = await host.Client.PostAsJsonAsync("Auth/Refresh", Credential(first));
        var next = await ReadTokens(refresh);
        Assert.NotEqual(first.RefreshToken, next.RefreshToken);
        using var logout = await host.Client.PostAsJsonAsync("Auth/Logout", Credential(next));
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        using var denied = await host.ProtectedAsync(first.AccessToken);
        await AssertError(denied, AuthenticationErrorCodes.SessionInvalid);
    }

    [Theory]
    [InlineData("Login")]
    [InlineData("Refresh")]
    [InlineData("Logout")]
    public async Task Browser_mutations_require_both_csrf_cookie_and_request_token(string action)
    {
        await using var host = await HttpHost.CreateAsync();
        using var without = await host.BrowserPostAsync(action, action == "Login" ? Credentials() : new RefreshRequestDto(), csrf: false);
        Assert.Equal(HttpStatusCode.BadRequest, without.StatusCode);
        await host.LoadCsrfAsync();
        using var cookieOnly = await host.BrowserPostAsync(action, action == "Login" ? Credentials() : new RefreshRequestDto(), csrf: false);
        Assert.Equal(HttpStatusCode.BadRequest, cookieOnly.StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Browser_cookie_is_secure_httponly_path_scoped_and_the_refresh_secret_never_enters_json(bool remember)
    {
        await using var host = await HttpHost.CreateAsync();
        await host.LoadCsrfAsync();
        using var login = await host.BrowserPostAsync("Login", Credentials(remember));
        var tokens = await ReadTokens(login);
        Assert.Null(tokens.RefreshToken);
        using var json = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.TryGetProperty("refreshToken", out _));
        var cookie = SetCookieHeaderValue.ParseList(login.Headers.GetValues("Set-Cookie").ToList())
            .Single(item => item.Name == BrowserAuthController.RefreshCookieName);
        Assert.True(cookie.HttpOnly);
        Assert.True(cookie.Secure);
        Assert.Equal(Microsoft.Net.Http.Headers.SameSiteMode.Strict, cookie.SameSite);
        Assert.Equal("/clinic/Auth/Browser", cookie.Path.ToString());
        Assert.Equal(remember, cookie.Expires.HasValue);
        using var restored = await host.BrowserPostAsync("Refresh", new RefreshRequestDto());
        var next = await ReadTokens(restored);
        Assert.Equal(tokens.SessionId, next.SessionId);
        Assert.Null(next.RefreshToken);
        Assert.NotEqual(cookie.Value.ToString(), host.Cookies.GetCookies(host.BrowserUri)[BrowserAuthController.RefreshCookieName]!.Value);
    }

    [Fact]
    public async Task Browser_and_native_refresh_credentials_cannot_be_interchanged()
    {
        await using var host = await HttpHost.CreateAsync();
        await host.LoadCsrfAsync();
        using var login = await host.BrowserPostAsync("Login", Credentials());
        var tokens = await ReadTokens(login);
        var refreshSecret = host.Cookies.GetCookies(host.BrowserUri)[BrowserAuthController.RefreshCookieName]!.Value;
        using var wrongEndpoint = await host.Client.PostAsJsonAsync("Auth/Refresh", new RefreshRequestDto
        {
            SessionId = tokens.SessionId, RefreshToken = refreshSecret
        });
        await AssertError(wrongEndpoint, AuthenticationErrorCodes.SessionInvalid);
        using var stillUsable = await host.BrowserPostAsync("Refresh", new RefreshRequestDto { SessionId = tokens.SessionId });
        Assert.Equal(HttpStatusCode.OK, stillUsable.StatusCode);
    }

    [Fact]
    public async Task A_stale_browser_tab_cannot_refresh_or_log_out_the_new_account()
    {
        await using var host = await HttpHost.CreateAsync();
        await host.LoadCsrfAsync();
        using var firstLogin = await host.BrowserPostAsync("Login", Credentials());
        var old = await ReadTokens(firstLogin);
        using var secondLogin = await host.BrowserPostAsync("Login", Credentials());
        var current = await ReadTokens(secondLogin);
        foreach (var action in new[] { "Refresh", "Logout" })
        {
            using var stale = await host.BrowserPostAsync(action, new RefreshRequestDto { SessionId = old.SessionId });
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            Assert.False(stale.Headers.Contains("Set-Cookie"));
        }
        using var active = await host.ProtectedAsync(current.AccessToken);
        Assert.Equal(HttpStatusCode.OK, active.StatusCode);
        using var replaced = await host.ProtectedAsync(old.AccessToken);
        await AssertError(replaced, AuthenticationErrorCodes.SessionInvalid);
    }

    [Fact]
    public async Task Expired_access_has_a_distinct_error_and_a_saved_refresh_still_works()
    {
        await using var host = await HttpHost.CreateAsync();
        using var login = await host.Client.PostAsJsonAsync("Auth/Login", Credentials());
        var tokens = await ReadTokens(login);
        await using var scope = host.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var session = await db.AuthenticationSessions.SingleAsync();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(session.UserId);
        var expired = await scope.ServiceProvider.GetRequiredService<JwtService>().GenerateJwtToken(
            user!, session.Id, session.SecurityStampFingerprint, DateTime.UtcNow.AddMinutes(-20), DateTime.UtcNow.AddMinutes(-1));
        using var rejected = await host.ProtectedAsync(expired);
        await AssertError(rejected, AuthenticationErrorCodes.AccessTokenExpired);
        using var refreshed = await host.Client.PostAsJsonAsync("Auth/Refresh", Credential(tokens));
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
    }

    [Fact]
    public async Task Security_stamp_change_invalidates_all_sessions_and_cannot_be_adopted_by_old_session_signing()
    {
        await using var host = await HttpHost.CreateAsync();
        using var firstLogin = await host.Client.PostAsJsonAsync("Auth/Login", Credentials());
        using var secondLogin = await host.Client.PostAsJsonAsync("Auth/Login", Credentials());
        var first = await ReadTokens(firstLogin);
        var second = await ReadTokens(secondLogin);
        await using var scope = host.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var session = await db.AuthenticationSessions.SingleAsync(item => item.Id == first.SessionId);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(session.UserId);
        Assert.True((await users.UpdateSecurityStampAsync(user!)).Succeeded);
        // 模拟验证旧会话后、真正签发前发生改密：签发仍必须携带旧会话的固定安全戳。
        var racedToken = await scope.ServiceProvider.GetRequiredService<JwtService>().GenerateJwtToken(
            user!, session.Id, session.SecurityStampFingerprint, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(15));
        foreach (var token in new[] { first.AccessToken, second.AccessToken, racedToken })
        {
            using var denied = await host.ProtectedAsync(token);
            await AssertError(denied, AuthenticationErrorCodes.SessionInvalid);
        }
        using var refresh = await host.Client.PostAsJsonAsync("Auth/Refresh", Credential(second));
        await AssertError(refresh, AuthenticationErrorCodes.SessionInvalid);
    }

    private static LoginRequestDto Credentials(bool remember = true) => new()
    {
        UserName = "http-user", Password = "test-password", RememberLogin = remember
    };
    private static RefreshRequestDto Credential(AuthenticationTokensDto tokens) => new()
    {
        SessionId = tokens.SessionId, RefreshToken = tokens.RefreshToken
    };
    private static async Task<AuthenticationTokensDto> ReadTokens(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthenticationTokensDto>())!;
    }
    private static async Task AssertError(HttpResponseMessage response, string code)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(code, (await response.Content.ReadFromJsonAsync<AuthenticationErrorDto>())?.Code);
    }

    /// <summary>复用生产认证配置与控制器，只替换数据库、邮件发送及限流配额。</summary>
    private sealed class HttpHost(WebApplication app, SqliteConnection connection, HttpClient client) : IAsyncDisposable
    {
        internal WebApplication App { get; } = app;
        internal HttpClient Client { get; } = client;
        internal CookieContainer Cookies { get; } = new();
        internal Uri BrowserUri { get; } = new("https://localhost/clinic/Auth/Browser/");
        private string? csrfToken;

        internal static async Task<HttpHost> CreateAsync()
        {
            using var rsa = RSA.Create(2048);
            var privateKey = Convert.ToBase64String(rsa.ExportPkcs8PrivateKey());
            var publicKey = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
            var connection = new SqliteConnection($"Data Source={Guid.NewGuid():N};Mode=Memory;Cache=Shared");
            await connection.OpenAsync();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Production,
                ApplicationName = typeof(AuthController).Assembly.GetName().Name
            });
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["JwtSettings:PublicKey"] = publicKey });
            builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection.ConnectionString));
            builder.AuthorizeConfiguration();
            builder.Services.AddControllersWithViews().AddApplicationPart(typeof(AuthController).Assembly);
            builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
            builder.Services.AddAntiforgery(options =>
            {
                options.HeaderName = "X-CSRF-TOKEN";
                options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            });
            builder.Services.Configure<JwtSettings>(options => { options.PrivateKey = privateKey; options.PublicKey = publicKey; });
            builder.Services.AddScoped<JwtService>();
            builder.Services.AddScoped<AuthenticationSessionService>();
            builder.Services.AddScoped<AuthManagerRepository>();
            builder.Services.AddScoped<AccountSecurityService>();
            builder.Services.AddScoped<AccountDeletionService>();
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddSingleton<LoginTimingEqualizer>();
            builder.Services.AddSingleton<CertificateFileStore>();
            builder.Services.AddSingleton<IAccountEmailSender, NoEmailSender>();
            builder.Services.AddSingleton<IAccountRecoveryQueue, AccountRecoveryQueue>();
            builder.Services.AddRateLimiter(options =>
            {
                foreach (var policy in new[] { "Login", "Refresh", "AccountRecovery" })
                    options.AddPolicy(policy, _ => RateLimitPartition.GetNoLimiter("test"));
            });
            var app = builder.Build();
            app.UsePathBase("/clinic");
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseRateLimiter();
            app.MapControllers();
            app.MapGet("/protected", () => Results.Ok()).RequireAuthorization();
            await using (var scope = app.Services.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreatedAsync();
                var result = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(
                    new ApplicationUser { UserName = "http-user", Email = "http@example.test", EmailConfirmed = true }, "test-password");
                Assert.True(result.Succeeded);
            }
            await app.StartAsync();
            var client = app.GetTestClient();
            client.BaseAddress = new Uri("https://localhost/clinic/");
            return new HttpHost(app, connection, client);
        }

        internal async Task LoadCsrfAsync()
        {
            using var response = await Client.GetAsync("Auth/Browser/Csrf");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            AcceptCookies(response);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            csrfToken = json.RootElement.GetProperty("requestToken").GetString();
        }

        internal async Task<HttpResponseMessage> BrowserPostAsync(string action, object body, bool csrf = true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "Auth/Browser/" + action) { Content = JsonContent.Create(body) };
            request.Headers.TryAddWithoutValidation("Cookie", Cookies.GetCookieHeader(BrowserUri));
            if (csrf) request.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", csrfToken);
            var response = await Client.SendAsync(request);
            AcceptCookies(response);
            return response;
        }

        internal Task<HttpResponseMessage> ProtectedAsync(string token)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "protected");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return Client.SendAsync(request);
        }

        private void AcceptCookies(HttpResponseMessage response)
        {
            if (response.Headers.TryGetValues("Set-Cookie", out var values))
                foreach (var value in values) Cookies.SetCookies(BrowserUri, value);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class NoEmailSender : IAccountEmailSender
    {
        public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendEmailChangeLinkAsync(ApplicationUser user, string newEmail, string confirmationLink, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendEmailChangedNotificationAsync(ApplicationUser user, string previousEmail, string newEmail, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
