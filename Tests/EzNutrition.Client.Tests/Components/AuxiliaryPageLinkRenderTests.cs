using System.Net;
using EzNutrition.Presentation.Services;
using EzNutrition.Presentation.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EzNutrition.Client.Tests.Components;

/// <summary>
/// 验证辅助页面链接只在宿主声明原生窗口能力时接管浏览器导航。
/// </summary>
public sealed class AuxiliaryPageLinkRenderTests
{
    /// <summary>
    /// 浏览器宿主应保留标准新标签页链接，供浏览器处理点击、复制与快捷菜单。
    /// </summary>
    [Theory]
    [InlineData(AuxiliaryPage.UserAgreement, "user-agreement")]
    [InlineData(AuxiliaryPage.PrivacyPolicy, "privacy-policy")]
    public async Task Browser_host_renders_native_new_tab_link(
        AuxiliaryPage page,
        string expectedPath)
    {
        var html = await RenderAsync(page, canOpenInNativeWindow: false);

        Assert.Contains($"href=\"{expectedPath}\"", html, StringComparison.Ordinal);
        Assert.Contains("target=\"_blank\"", html, StringComparison.Ordinal);
        Assert.Contains("rel=\"noopener noreferrer\"", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// 多窗口宿主应渲染由 Blazor 接管的链接，而不再要求浏览器创建标签页。
    /// </summary>
    [Fact]
    public async Task Native_window_host_does_not_render_a_blank_target()
    {
        var html = await RenderAsync(
            AuxiliaryPage.UserAgreement,
            canOpenInNativeWindow: true);

        Assert.Contains("href=\"user-agreement\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("target=", html, StringComparison.Ordinal);
        Assert.DoesNotContain("rel=", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// 未定义的页面值不应退化为一个可能误导用户的链接。
    /// </summary>
    [Fact]
    public void Undefined_page_has_no_route()
    {
        var page = (AuxiliaryPage)0;

        Assert.Throws<ArgumentOutOfRangeException>(() => page.GetRelativePath());
        Assert.Throws<ArgumentOutOfRangeException>(() => page.GetTitle());
    }

    private static async Task<string> RenderAsync(
        AuxiliaryPage page,
        bool canOpenInNativeWindow)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IAuxiliaryPageHost>(
            new StubAuxiliaryPageHost(canOpenInNativeWindow));
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(
            provider,
            provider.GetRequiredService<ILoggerFactory>());

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = ParameterView.FromDictionary(
                new Dictionary<string, object?>
                {
                    [nameof(AuxiliaryPageLink.Page)] = page,
                    [nameof(AuxiliaryPageLink.ChildContent)] =
                        (RenderFragment)(builder => builder.AddContent(0, "查看文档"))
                });
            var output = await renderer.RenderComponentAsync<AuxiliaryPageLink>(parameters);
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });
    }

    private sealed class StubAuxiliaryPageHost(bool canOpenInNativeWindow) :
        IAuxiliaryPageHost
    {
        public bool CanOpenInNativeWindow { get; } = canOpenInNativeWindow;

        public ValueTask OpenInNativeWindowAsync(AuxiliaryPage page) =>
            ValueTask.CompletedTask;
    }
}
