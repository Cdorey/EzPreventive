using System.Net;
using AntDesign;
using EzNutrition.UI.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace EzNutrition.Client.Tests.Components;

public sealed class SoapTextSelectionImportRenderTests
{
    [Fact]
    public async Task Enabled_import_renders_review_actions_around_source_content()
    {
        await using var services = BuildServiceProvider();
        await using var renderer = new HtmlRenderer(
            services,
            services.GetRequiredService<ILoggerFactory>());

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<SoapTextSelectionImport>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(SoapTextSelectionImport.Enabled)] = true,
                    [nameof(SoapTextSelectionImport.ChildContent)] = SourceContent("AI 草稿正文"),
                    [nameof(SoapTextSelectionImport.OnAssessmentConfirmed)] =
                        EventCallback.Factory.Create<string>(new object(), _ => { }),
                    [nameof(SoapTextSelectionImport.OnPlanConfirmed)] =
                        EventCallback.Factory.Create<string>(new object(), _ => { })
                }));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });

        Assert.Contains("AI 草稿正文", html, StringComparison.Ordinal);
        Assert.Contains("先划选上方 AI 文本", html, StringComparison.Ordinal);
        Assert.Contains("引入 A · 问题评估", html, StringComparison.Ordinal);
        Assert.Contains("引入 P · 处理计划", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Disabled_import_preserves_source_without_rendering_actions()
    {
        await using var services = BuildServiceProvider();
        await using var renderer = new HtmlRenderer(
            services,
            services.GetRequiredService<ILoggerFactory>());

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<SoapTextSelectionImport>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(SoapTextSelectionImport.Enabled)] = false,
                    [nameof(SoapTextSelectionImport.ChildContent)] = SourceContent("尚未完成的 AI 草稿"),
                    [nameof(SoapTextSelectionImport.OnAssessmentConfirmed)] =
                        EventCallback.Factory.Create<string>(new object(), _ => { }),
                    [nameof(SoapTextSelectionImport.OnPlanConfirmed)] =
                        EventCallback.Factory.Create<string>(new object(), _ => { })
                }));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });

        Assert.Contains("尚未完成的 AI 草稿", html, StringComparison.Ordinal);
        Assert.DoesNotContain("引入 A · 问题评估", html, StringComparison.Ordinal);
        Assert.DoesNotContain("引入 P · 处理计划", html, StringComparison.Ordinal);
    }

    private static RenderFragment SourceContent(string content) => builder =>
    {
        builder.OpenElement(0, "p");
        builder.AddContent(1, content);
        builder.CloseElement();
    };

    private static ServiceProvider BuildServiceProvider() => new ServiceCollection()
        .AddLogging()
        .AddAntDesign()
        .AddSingleton<IJSRuntime, NoOpJsRuntime>()
        .BuildServiceProvider();

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
