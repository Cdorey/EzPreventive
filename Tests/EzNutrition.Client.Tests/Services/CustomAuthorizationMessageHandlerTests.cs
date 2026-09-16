using EzNutrition.Application.Ports;
using EzNutrition.Presentation.Infrastructure;
using EzNutrition.Presentation.Services;
using EzNutrition.Shared.Data.DTO;
using Microsoft.AspNetCore.Components;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace EzNutrition.Client.Tests.Services;

/// <summary>验证 HTTP 身份附加、受控重试和跨账号响应隔离。</summary>
public sealed class CustomAuthorizationMessageHandlerTests
{
    [Fact]
    public async Task An_expired_access_token_is_refreshed_and_retried_only_once()
    {
        var context = new SessionTestContext();
        var original = await context.SignInAsync();
        List<string?> sent = [];
        using var client = CreateClient(context, new DelegateHandler(request =>
        {
            sent.Add(request.Headers.Authorization?.Parameter);
            return Task.FromResult(Error(AuthenticationErrorCodes.AccessTokenExpired));
        }));
        using var response = await client.GetAsync("User/Profile");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(2, sent.Count);
        Assert.Equal(original.AccessToken, sent[0]);
        Assert.NotEqual(sent[0], sent[1]);
        Assert.Equal(1, context.Authentication.RefreshCount);
        Assert.NotNull(context.Session.UserInfo);
    }

    [Theory]
    [InlineData("POST", false, false, 1)]
    [InlineData("POST", true, false, 2)]
    [InlineData("POST", true, true, 1)]
    [InlineData("GET", false, true, 1)]
    public async Task Only_repeatable_and_explicitly_safe_requests_can_be_retried(
        string method, bool allowRetry, bool streamContent, int expectedRequests)
    {
        var context = new SessionTestContext();
        await context.SignInAsync();
        var count = 0;
        List<string> bodies = [];
        using var client = CreateClient(context, new DelegateHandler(async request =>
        {
            count++;
            bodies.Add(await request.Content!.ReadAsStringAsync());
            return count == 1 ? Error(AuthenticationErrorCodes.AccessTokenExpired) : new HttpResponseMessage(HttpStatusCode.OK);
        }));
        using var request = new HttpRequestMessage(new HttpMethod(method), "AiAdvice")
        {
            Content = streamContent
                ? new StreamContent(new MemoryStream("immutable-request"u8.ToArray()))
                : new StringContent("immutable-request")
        };
        request.Options.Set(CustomAuthorizationMessageHandler.AllowAuthenticationRetry, allowRetry);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(expectedRequests, count);
        Assert.All(bodies, body => Assert.Equal("immutable-request", body));
        Assert.Equal(expectedRequests - 1, context.Authentication.RefreshCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task Forbidden_or_unclassified_unauthorized_does_not_trigger_refresh(HttpStatusCode status)
    {
        var context = new SessionTestContext();
        await context.SignInAsync();
        var count = 0;
        using var client = CreateClient(context, new DelegateHandler(_ =>
        {
            count++;
            return Task.FromResult(new HttpResponseMessage(status));
        }));
        using var response = await client.GetAsync("User/Profile");
        Assert.Equal(1, count);
        Assert.Equal(0, context.Authentication.RefreshCount);
        Assert.NotNull(context.Session.UserInfo);
    }

    [Fact]
    public async Task A_revoked_current_session_is_cleared_and_redirected()
    {
        var context = new SessionTestContext();
        await context.SignInAsync();
        var navigation = new RecordingNavigationManager();
        using var client = CreateClient(context,
            new DelegateHandler(_ => Task.FromResult(Error(AuthenticationErrorCodes.SessionInvalid))), navigation);
        using var response = await client.GetAsync("User/Profile");
        Assert.Null(context.Session.UserInfo);
        Assert.Equal(1, navigation.NavigationCount);
        Assert.Equal(1, context.Authentication.SignOutCount);
    }

    [Theory]
    [InlineData(AuthenticationErrorCodes.AccessTokenExpired, HttpStatusCode.Conflict, AuthenticationErrorCodes.SessionChanged)]
    [InlineData(AuthenticationErrorCodes.SessionInvalid, HttpStatusCode.Unauthorized, AuthenticationErrorCodes.SessionInvalid)]
    public async Task A_delayed_old_account_response_cannot_replay_or_clear_the_new_account(
        string errorCode, HttpStatusCode expectedStatus, string expectedCode)
    {
        var context = new SessionTestContext();
        await context.SignInAsync("old");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        using var client = CreateClient(context, new DelegateHandler(async _ =>
        {
            count++;
            entered.TrySetResult();
            await released.Task;
            return Error(errorCode);
        }));
        var pending = client.GetAsync("User/Profile");
        await entered.Task;
        var current = await context.SignInAsync("new");
        released.SetResult();
        using var response = await pending;
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(expectedCode, (await response.Content.ReadFromJsonAsync<AuthenticationErrorDto>())?.Code);
        Assert.Equal(1, count);
        Assert.Equal(current.AccessToken, await context.Session.GetValidAccessTokenAsync());
        Assert.Equal(0, context.Authentication.RefreshCount);
        Assert.Equal(0, context.Authentication.SignOutCount);
    }

    [Fact]
    public async Task External_endpoints_and_explicit_authorization_are_untouched()
    {
        var context = new SessionTestContext();
        await context.SignInAsync();
        List<AuthenticationHeaderValue?> sent = [];
        using var client = CreateClient(context, new DelegateHandler(request =>
        {
            sent.Add(request.Headers.Authorization);
            return Task.FromResult(Error(AuthenticationErrorCodes.SessionInvalid));
        }));
        using var external = await client.GetAsync("https://outside.example.test/resource");
        using var explicitRequest = new HttpRequestMessage(HttpMethod.Get, "User/Profile");
        explicitRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", "provided-by-caller");
        using var explicitResponse = await client.SendAsync(explicitRequest);
        Assert.Null(sent[0]);
        Assert.Equal("Basic", sent[1]?.Scheme);
        Assert.Equal("provided-by-caller", sent[1]?.Parameter);
        Assert.NotNull(context.Session.UserInfo);
        Assert.Equal(0, context.Authentication.SignOutCount);
    }

    [Fact]
    public async Task An_expired_token_and_offline_refresh_does_not_send_an_unauthenticated_business_request()
    {
        var context = new SessionTestContext();
        await context.SignInAsync();
        context.Clock.Advance(TimeSpan.FromMinutes(16));
        context.Authentication.Refresh = _ => throw new HttpRequestException("offline");
        using var client = CreateClient(context, new DelegateHandler(_ => throw new InvalidOperationException("业务请求不应发送。")));
        using var response = await client.GetAsync("User/Profile");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.NotNull(context.Session.UserInfo);
    }

    /// <summary>刷新失败在业务请求发出前返回时，仍须保留协议约定的状态码和错误正文。</summary>
    [Theory]
    [InlineData(AuthenticationErrorCodes.SessionChanged, HttpStatusCode.Conflict)]
    [InlineData(AuthenticationErrorCodes.SessionInvalid, HttpStatusCode.Unauthorized)]
    public async Task Refresh_rejection_preserves_its_protocol_status_and_error_code(string code, HttpStatusCode expectedStatus)
    {
        var context = new SessionTestContext();
        await context.SignInAsync();
        context.Clock.Advance(TimeSpan.FromMinutes(16));
        context.Authentication.Refresh = _ => throw new SessionAuthenticationException(code, "测试刷新拒绝");
        using var client = CreateClient(context, new DelegateHandler(_ => throw new InvalidOperationException("业务请求不应发送。")));

        using var response = await client.GetAsync("User/Profile");

        Assert.Equal(expectedStatus, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<AuthenticationErrorDto>();
        Assert.Equal(code, error?.Code);
        Assert.Equal("测试刷新拒绝", error?.Message);
        Assert.Equal(1, context.Authentication.RefreshCount);
    }

    /// <summary>通过真实处理器与 AI 网关验证账号冲突不会被归类为权限不足。</summary>
    [Fact]
    public async Task Ai_gateway_classifies_a_session_conflict_as_request_rejection()
    {
        var context = new SessionTestContext();
        await context.SignInAsync();
        context.Clock.Advance(TimeSpan.FromMinutes(16));
        context.Authentication.Refresh = _ => throw new SessionAuthenticationException(
            AuthenticationErrorCodes.SessionChanged, "账号已切换");
        using var client = CreateClient(context, new DelegateHandler(_ => throw new InvalidOperationException("业务请求不应发送。")));
        var gateway = new HttpAiAdviceGateway(new ClientFactory(client));

        var error = await Assert.ThrowsAsync<AiAdviceAccessException>(() => gateway.GetEnvironmentAsync());

        Assert.Equal(AiAdviceAccessFailureKind.Rejected, error.FailureKind);
        Assert.Contains("409", error.Message, StringComparison.Ordinal);
    }

    private static HttpResponseMessage Error(string code) => new(HttpStatusCode.Unauthorized)
    {
        Content = JsonContent.Create(new AuthenticationErrorDto(code, "test authentication error"))
    };

    private static HttpClient CreateClient(SessionTestContext context, HttpMessageHandler terminal,
        RecordingNavigationManager? navigation = null) => new(new CustomAuthorizationMessageHandler(
            context.Session, navigation ?? new RecordingNavigationManager(), new ApplicationServerEndpoint(SessionTestContext.BaseAddress))
        { InnerHandler = terminal }) { BaseAddress = SessionTestContext.BaseAddress };

    private sealed class ClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class DelegateHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
    }

    private sealed class RecordingNavigationManager : NavigationManager
    {
        internal RecordingNavigationManager() => Initialize("https://0.0.0.0/", "https://0.0.0.0/");
        internal int NavigationCount { get; private set; }
        protected override void NavigateToCore(string uri, bool forceLoad) => NavigationCount++;
        protected override void NavigateToCore(string uri, NavigationOptions options) => NavigationCount++;
    }
}
