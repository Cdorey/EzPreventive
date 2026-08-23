using EzNutrition.Client.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging.Abstractions;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;

namespace EzNutrition.Client.Tests.Services;

public sealed class CustomAuthorizationMessageHandlerTests
{
    [Fact]
    public async Task Unauthorized_for_same_origin_request_with_current_token_clears_the_session()
    {
        var token = CreateToken("current-user");
        var session = CreateSession(token);
        await session.SignInAsync("current-user", "password");
        var navigation = new RecordingNavigationManager();
        var terminal = new StatusCodeHandler(HttpStatusCode.Unauthorized);
        using var client = CreateAuthorizedClient(session, navigation, terminal);

        using var response = await client.GetAsync("User/Profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer", terminal.Authorization?.Scheme);
        Assert.Equal(token, terminal.Authorization?.Parameter);
        Assert.False(session.TryGetAccessToken(out _));
        Assert.Equal(1, navigation.NavigationCount);
    }

    [Fact]
    public async Task Unauthorized_without_an_attached_token_does_not_clear_the_session()
    {
        var token = CreateToken("current-user");
        var session = CreateSession(token);
        await session.SignInAsync("current-user", "password");
        var navigation = new RecordingNavigationManager();
        var terminal = new StatusCodeHandler(HttpStatusCode.Unauthorized);
        using var client = CreateAuthorizedClient(session, navigation, terminal);

        using var response = await client.GetAsync("https://outside.example.test/resource");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(terminal.Authorization);
        Assert.True(session.TryGetAccessToken(out var currentToken));
        Assert.Equal(token, currentToken);
        Assert.Equal(0, navigation.NavigationCount);
    }

    [Fact]
    public async Task Forbidden_with_the_current_token_does_not_clear_the_session()
    {
        var token = CreateToken("current-user");
        var session = CreateSession(token);
        await session.SignInAsync("current-user", "password");
        var navigation = new RecordingNavigationManager();
        var terminal = new StatusCodeHandler(HttpStatusCode.Forbidden);
        using var client = CreateAuthorizedClient(session, navigation, terminal);

        using var response = await client.GetAsync("User/Profile");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(token, terminal.Authorization?.Parameter);
        Assert.True(session.TryGetAccessToken(out var currentToken));
        Assert.Equal(token, currentToken);
        Assert.Equal(0, navigation.NavigationCount);
    }

    [Fact]
    public async Task Unauthorized_from_an_old_request_does_not_clear_a_new_session()
    {
        var oldToken = CreateToken("old-user");
        var newToken = CreateToken("new-user");
        var session = CreateSession(oldToken, newToken);
        await session.SignInAsync("old-user", "password");
        var navigation = new RecordingNavigationManager();
        var terminal = new DelayedUnauthorizedHandler();
        using var client = CreateAuthorizedClient(session, navigation, terminal);

        var pendingRequest = client.GetAsync("User/Profile");
        await terminal.RequestObserved;
        Assert.Equal(oldToken, terminal.Authorization?.Parameter);

        await session.SignInAsync("new-user", "password");
        terminal.ReleaseResponse();
        using var response = await pendingRequest;

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(session.TryGetAccessToken(out var currentToken));
        Assert.Equal(newToken, currentToken);
        Assert.Equal(0, navigation.NavigationCount);
    }

    [Fact]
    public async Task Api_endpoint_is_independent_from_the_hybrid_navigation_origin()
    {
        var token = CreateToken("desktop-user");
        var session = CreateSession(token);
        await session.SignInAsync("desktop-user", "password");
        var navigation = new RecordingNavigationManager(new Uri("https://0.0.0.0/"));
        var terminal = new StatusCodeHandler(HttpStatusCode.OK);
        using var client = CreateAuthorizedClient(session, navigation, terminal);

        using var response = await client.GetAsync("User/Profile");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(token, terminal.Authorization?.Parameter);
    }

    private static UserSessionService CreateSession(params string[] loginTokens)
    {
        var loginHandler = new QueuedLoginHandler(loginTokens);
        var client = new HttpClient(loginHandler)
        {
            BaseAddress = RecordingNavigationManager.ApplicationBaseAddress
        };
        return new UserSessionService(
            new StaticHttpClientFactory(client),
            NullLogger<UserSessionService>.Instance);
    }

    private static HttpClient CreateAuthorizedClient(
        UserSessionService session,
        RecordingNavigationManager navigation,
        HttpMessageHandler terminal)
    {
        var authorization = new CustomAuthorizationMessageHandler(
            session,
            navigation,
            new ApplicationServerEndpoint(RecordingNavigationManager.ApplicationBaseAddress))
        {
            InnerHandler = terminal
        };
        return new HttpClient(authorization)
        {
            BaseAddress = RecordingNavigationManager.ApplicationBaseAddress
        };
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

    private sealed class QueuedLoginHandler(IEnumerable<string> tokens) : HttpMessageHandler
    {
        private readonly Queue<string> tokens = new(tokens);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(
                new Uri(RecordingNavigationManager.ApplicationBaseAddress, "Auth/Login"),
                request.RequestUri);
            Assert.True(tokens.TryDequeue(out var token), "No login token was queued for this request.");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(token)
            });
        }
    }

    private sealed class StatusCodeHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        internal AuthenticationHeaderValue? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization;
            return Task.FromResult(new HttpResponseMessage(statusCode));
        }
    }

    private sealed class DelayedUnauthorizedHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource requestObserved = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource releaseResponse = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        internal Task RequestObserved => requestObserved.Task;

        internal AuthenticationHeaderValue? Authorization { get; private set; }

        internal void ReleaseResponse() => releaseResponse.TrySetResult();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization;
            requestObserved.TrySetResult();
            await releaseResponse.Task.WaitAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }
    }

    private sealed class RecordingNavigationManager : NavigationManager
    {
        internal static readonly Uri ApplicationBaseAddress = new("https://app.example.test/");

        internal RecordingNavigationManager()
            : this(ApplicationBaseAddress)
        {
        }

        internal RecordingNavigationManager(Uri baseAddress)
        {
            Initialize(baseAddress.AbsoluteUri, baseAddress.AbsoluteUri);
        }

        internal int NavigationCount { get; private set; }

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            NavigationCount++;
            Uri = ToAbsoluteUri(uri).AbsoluteUri;
        }

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
            NavigationCount++;
            Uri = ToAbsoluteUri(uri).AbsoluteUri;
        }
    }
}
