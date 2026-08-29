using System.Net;
using System.Net.Http.Json;
using EzNutrition.Application.Archives;
using EzNutrition.Archives.Contracts.ValueObjects;
using EzNutrition.Presentation;
using EzNutrition.Presentation.Services;
using EzNutrition.Presentation.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace EzNutrition.Client.Tests.Components;

/// <summary>
/// 验证应用级布局中的公共部署信息能够渲染为最终用户可见文本。
/// </summary>
public sealed class MainLayoutRenderTests
{
    /// <summary>
    /// 验证 Razor 不会把紧邻版本号的表达式误判为普通文本。
    /// </summary>
    [Fact]
    public async Task Footer_renders_resolved_client_and_server_versions()
    {
        await using var services = BuildServiceProvider();
        await using var renderer = new HtmlRenderer(
            services,
            services.GetRequiredService<ILoggerFactory>());

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            RenderFragment layout = builder =>
            {
                builder.OpenComponent<MainLayout>(0);
                builder.AddAttribute(
                    1,
                    nameof(MainLayout.Body),
                    (RenderFragment)(content => content.AddContent(0, "测试正文")));
                builder.CloseComponent();
            };
            var output = await renderer.RenderComponentAsync<CascadingAuthenticationState>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(CascadingAuthenticationState.ChildContent)] = layout
                }));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });

        Assert.Contains("前端 v2.1.0.0", html, StringComparison.Ordinal);
        Assert.Contains("后端 v2.1.0.0", html, StringComparison.Ordinal);
        Assert.DoesNotContain("v@UserSession", html, StringComparison.Ordinal);
    }

    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEzNutritionPresentation(
            new Uri("https://app.example.test/"),
            TimeZoneInfo.Utc,
            primaryHttpMessageHandlerFactory: static () => new PublicSystemInfoHandler());
        services.AddSingleton(provider => new UserSessionService(
            provider.GetRequiredService<IHttpClientFactory>(),
            provider.GetRequiredService<ILogger<UserSessionService>>(),
            credentialStore: null,
            clientVersion: "2.1.0.0"));
        services.AddCascadingAuthenticationState();
        services.AddSingleton<NavigationManager, TestNavigationManager>();
        services.AddSingleton<IJSRuntime, NoOpJsRuntime>();
        services.AddSingleton(new ArchiveContractAssembler(new ApplicationIdentity(
            new Uri("https://app.example.test/layout-render-test"),
            "布局渲染测试",
            "2.1-test")));
        return services.BuildServiceProvider();
    }

    private sealed class PublicSystemInfoHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = request.RequestUri?.AbsolutePath.EndsWith(
                "/SystemInfo/PublicInfo/",
                StringComparison.Ordinal) == true
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        caseNumber = "test-case-number",
                        serverVersion = "2.1.0.0"
                    })
                }
                : new HttpResponseMessage(HttpStatusCode.NotFound);
            return Task.FromResult(response);
        }
    }

    private sealed class NoOpJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            ValueTask.FromResult(default(TValue)!);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args) => ValueTask.FromResult(default(TValue)!);
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager()
        {
            Initialize("https://app.example.test/", "https://app.example.test/");
        }

        protected override void NavigateToCore(string uri, bool forceLoad) =>
            Uri = ToAbsoluteUri(uri).AbsoluteUri;

        protected override void NavigateToCore(string uri, NavigationOptions options) =>
            Uri = ToAbsoluteUri(uri).AbsoluteUri;
    }
}
