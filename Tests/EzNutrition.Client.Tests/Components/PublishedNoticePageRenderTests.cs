using System.Net;
using System.Net.Http.Json;
using AntDesign;
using EzNutrition.Presentation.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using PrivacyPolicyPage = EzNutrition.Presentation.Pages.PrivacyPolicy;
using UserAgreementPage = EzNutrition.Presentation.Pages.UserAgreement;

namespace EzNutrition.Client.Tests.Components;

/// <summary>
/// 验证公开政策页面只渲染各自对应的服务端 Markdown 内容。
/// </summary>
public sealed class PublishedNoticePageRenderTests
{
    [Fact]
    public async Task Agreement_and_privacy_pages_render_their_respective_documents()
    {
        await using var services = BuildServiceProvider();
        await services.GetRequiredService<UserSessionService>().GetSystemInfoAsync();

        var agreementHtml = await RenderAsync<UserAgreementPage>(services);
        var privacyHtml = await RenderAsync<PrivacyPolicyPage>(services);

        Assert.Contains("用户许可协议", agreementHtml, StringComparison.Ordinal);
        Assert.Contains("协议正文", agreementHtml, StringComparison.Ordinal);
        Assert.Contains("<strong>必须阅读</strong>", agreementHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("隐私正文", agreementHtml, StringComparison.Ordinal);

        Assert.Contains("隐私条款", privacyHtml, StringComparison.Ordinal);
        Assert.Contains("隐私正文", privacyHtml, StringComparison.Ordinal);
        Assert.Contains("<strong>谨慎处理</strong>", privacyHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("协议正文", privacyHtml, StringComparison.Ordinal);
    }

    [Fact]
    public void Pages_expose_stable_public_routes()
    {
        Assert.Equal("/user-agreement", GetRoute<UserAgreementPage>());
        Assert.Equal("/privacy-policy", GetRoute<PrivacyPolicyPage>());
    }

    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAntDesign();
        services.AddSingleton<IJSRuntime, NoOpJsRuntime>();
        services.AddSingleton<IHttpClientFactory>(_ => new StaticHttpClientFactory(
            new HttpClient(new PublicContentHandler())
            {
                BaseAddress = new Uri("https://app.example.test/")
            }));
        services.AddSingleton(provider => new UserSessionService(
            provider.GetRequiredService<IHttpClientFactory>(),
            provider.GetRequiredService<ILogger<UserSessionService>>(),
            clientVersion: "2.1.0.0"));
        return services.BuildServiceProvider();
    }

    private static async Task<string> RenderAsync<TComponent>(ServiceProvider services)
        where TComponent : IComponent
    {
        await using var renderer = new HtmlRenderer(
            services,
            services.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<TComponent>();
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });
    }

    private static string GetRoute<TComponent>() =>
        Assert.Single(typeof(TComponent).GetCustomAttributes(typeof(RouteAttribute), inherit: false))
            is RouteAttribute route
                ? route.Template
                : throw new InvalidOperationException("页面缺少路由。");

    private sealed class StaticHttpClientFactory(HttpClient client) : IHttpClientFactory, IDisposable
    {
        public HttpClient CreateClient(string name) => client;

        public void Dispose() => client.Dispose();
    }

    private sealed class PublicContentHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var content = request.RequestUri?.AbsolutePath switch
            {
                "/SystemInfo/PublicInfo/" => JsonContent.Create(new
                {
                    caseNumber = "test-case-number",
                    serverVersion = "2.1.0.0"
                }),
                "/SystemInfo/CoverLetter/" => NoticeContent("产品说明"),
                "/SystemInfo/Notice/" => NoticeContent("工作提示"),
                "/SystemInfo/UserAgreement/" => NoticeContent("# 协议正文\n\n**必须阅读**"),
                "/SystemInfo/PrivacyPolicy/" => NoticeContent("# 隐私正文\n\n**谨慎处理**"),
                _ => throw new InvalidOperationException($"Unexpected request URI: {request.RequestUri}")
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = content
            });
        }

        private static JsonContent NoticeContent(string description) =>
            JsonContent.Create(new { description });
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
}
