using EzNutrition.Presentation;
using EzNutrition.Presentation.Services;
using EzNutrition.Shared.Data.DTO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Net.Http.Json;

namespace EzNutrition.Client.Tests.Services;

/// <summary>验证公共信息的独立加载、并发合并及与认证流程的隔离。</summary>
public sealed class PublicSystemInfoServiceTests
{
    /// <summary>多个页面共享一轮请求；取消单次等待不影响其他页面取得内容。</summary>
    [Fact]
    public async Task Concurrent_initialization_shares_requests_and_survives_a_cancelled_waiter()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestCount = 0;
        using var client = new HttpClient(new DelegateHandler(async (request, cancellationToken) =>
        {
            if (Interlocked.Increment(ref requestCount) == 5) entered.TrySetResult();
            await released.Task.WaitAsync(cancellationToken);
            return PublicResponse(request);
        })) { BaseAddress = SessionTestContext.BaseAddress };
        var service = new PublicSystemInfoService(new ClientFactory(client),
            NullLogger<PublicSystemInfoService>.Instance, clientVersion: " 2.2.0.0 ");
        var notifications = 0;
        service.StateChanged += () => notifications++;
        using var cancellation = new CancellationTokenSource();

        var cancelled = service.InitializeAsync(cancellation.Token);
        var waiting = Enumerable.Range(0, 8).Select(_ => service.InitializeAsync()).ToArray();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        Assert.False(service.IsLoaded);
        Assert.Equal(0, notifications);
        released.SetResult();
        await Task.WhenAll(waiting).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(service.IsLoaded);
        Assert.Equal("test-case", service.CaseNumber);
        Assert.Equal("2.2.0.0", service.ClientVersion);
        Assert.Equal("2.2.0.0", service.ServerVersion);
        Assert.Equal("产品说明", service.CoverLetter);
        Assert.Equal("工作提示", service.Notice);
        Assert.Equal("许可协议", service.UserAgreement);
        Assert.Equal("隐私条款", service.PrivacyPolicy);
        Assert.False(service.HasVersionCompatibilityWarning);
        await service.InitializeAsync();
        Assert.Equal(5, requestCount);
        Assert.Equal(1, notifications);
    }

    /// <summary>单项内容加载失败时，其他公共信息仍可展示且加载状态正常结束。</summary>
    [Theory]
    [InlineData("network")]
    [InlineData("malformed")]
    [InlineData("timeout")]
    public async Task One_failed_document_does_not_hide_other_public_information(string failure)
    {
        using var client = new HttpClient(new DelegateHandler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath == "/SystemInfo/Notice/")
            {
                return failure switch
                {
                    "network" => throw new HttpRequestException("offline"),
                    "timeout" => throw new TaskCanceledException("timeout"),
                    _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("invalid-json", System.Text.Encoding.UTF8, "application/json")
                    })
                };
            }
            return Task.FromResult(PublicResponse(request));
        })) { BaseAddress = SessionTestContext.BaseAddress };
        var service = new PublicSystemInfoService(new ClientFactory(client), NullLogger<PublicSystemInfoService>.Instance);

        await service.InitializeAsync();

        Assert.True(service.IsLoaded);
        Assert.Empty(service.Notice);
        Assert.Equal("test-case", service.CaseNumber);
        Assert.Equal("许可协议", service.UserAgreement);
        Assert.Equal("隐私条款", service.PrivacyPolicy);
    }

    /// <summary>使用正式服务注册验证慢请求和失败不阻塞认证，登录退出也不重新加载公共内容。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Public_loading_is_independent_of_authentication_and_account_changes(bool unavailable)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestCount = 0;
        var tokens = SessionTestContext.CreateTokens(DateTimeOffset.UtcNow);
        var authentication = new TestAuthenticationClient
        {
            Restore = () => Task.FromResult<AuthenticationTokensDto?>(tokens),
            SignIn = request => Task.FromResult(SessionTestContext.CreateTokens(DateTimeOffset.UtcNow, request.UserName))
        };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IAuthenticationSessionClient>(authentication);
        services.AddEzNutritionPresentation(SessionTestContext.BaseAddress, TimeZoneInfo.Utc,
            primaryHttpMessageHandlerFactory: () => new DelegateHandler(async (request, cancellationToken) =>
            {
                if (Interlocked.Increment(ref requestCount) == 5) entered.TrySetResult();
                await released.Task.WaitAsync(cancellationToken);
                return unavailable
                    ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                    : PublicResponse(request);
            }));
        await using var provider = services.BuildServiceProvider();
        var systemInfo = provider.GetRequiredService<PublicSystemInfoService>();
        var session = provider.GetRequiredService<UserSessionService>();

        var loading = systemInfo.InitializeAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await session.InitializeAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(tokens.AccessToken, await session.GetValidAccessTokenAsync().WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True((await session.GetAuthenticationStateAsync()).User.Identity!.IsAuthenticated);
        Assert.False(systemInfo.IsLoaded);
        var authenticationNotifications = 0;
        session.StateChanged += () => authenticationNotifications++;
        released.SetResult();
        await loading.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(systemInfo.IsLoaded);
        Assert.Equal(unavailable ? string.Empty : "2.2.0.0", systemInfo.ServerVersion);
        Assert.Equal(0, authenticationNotifications);

        await session.SignOutAsync();
        await session.SignInAsync("another-user", "password");
        await Task.WhenAll(session.InitializeAsync(), systemInfo.InitializeAsync());
        Assert.Equal("another-user-id", session.UserInfo?.UserId);
        Assert.Equal(1, authentication.RestoreCount);
        Assert.Equal(5, requestCount);
    }

    /// <summary>版本兼容性仍按产品和契约代际判断，无法解析的版本不产生误报。</summary>
    [Theory]
    [InlineData("2.2.0.0", "2.2.99.42", false)]
    [InlineData("2.1.0.0", "2.2.0.0", true)]
    [InlineData("2.2.0.0", "3.2.0.0", true)]
    [InlineData("", "2.2.0.0", false)]
    [InlineData("2.2.0.0", "invalid", false)]
    public void Compatibility_warning_compares_product_and_contract_segments(string client, string server, bool expected) =>
        Assert.Equal(expected, PublicSystemInfoService.IsCompatibilityMismatch(client, server));

    private static HttpResponseMessage PublicResponse(HttpRequestMessage request)
    {
        Assert.Null(request.Headers.Authorization);
        var path = request.RequestUri!.AbsolutePath;
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = path == "/SystemInfo/PublicInfo/"
                ? JsonContent.Create(new { caseNumber = " test-case ", serverVersion = " 2.2.0.0 " })
                : JsonContent.Create(new
                {
                    description = path switch
                    {
                        "/SystemInfo/CoverLetter/" => "产品说明",
                        "/SystemInfo/Notice/" => "工作提示",
                        "/SystemInfo/UserAgreement/" => "许可协议",
                        "/SystemInfo/PrivacyPolicy/" => "隐私条款",
                        _ => throw new InvalidOperationException($"非预期的公开接口：{path}")
                    }
                })
        };
    }

    private sealed class ClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            Assert.Equal("Anonymous", name);
            return client;
        }
    }

    private sealed class DelegateHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
