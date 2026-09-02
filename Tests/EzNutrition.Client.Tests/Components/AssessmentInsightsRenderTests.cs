using System.Net;
using AntDesign;
using EzNutrition.Application.Consultations;
using EzNutrition.Assessments.Common;
using EzNutrition.Domain.Assessments;
using EzNutrition.Presentation.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace EzNutrition.Client.Tests.Components;

/// <summary>
/// 验证量表速查页从运行时注册目录解析量表，并复用通用量表交互入口。
/// </summary>
public sealed class AssessmentInsightsRenderTests
{
    /// <summary>
    /// 验证精确的代码体系、编码和版本能够打开注册量表，且明确其临时运行语义。
    /// </summary>
    [Fact]
    public async Task Registered_instrument_renders_as_a_standalone_assessment_tool()
    {
        var instrument = new ChasSgaInstrument();
        await using var services = BuildServiceProvider(instrument);

        var html = await RenderAsync(
            services,
            instrument.Definition.Code,
            instrument.Definition.CodeSystem.AbsoluteUri,
            instrument.Definition.Version);

        Assert.Contains("量表速查", html, StringComparison.Ordinal);
        Assert.Contains("主观整体评估 SGA（团标版）", html, StringComparison.Ordinal);
        Assert.Contains("本页用于独立速查", html, StringComparison.Ordinal);
        Assert.Contains("开始评估", html, StringComparison.Ordinal);
        Assert.Contains("不会加入咨询档案或 SOAP", html, StringComparison.Ordinal);
        Assert.DoesNotContain("引入 SOAP", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证未知版本不会退回同编码的其他实现，避免速查内容与导航标识不一致。
    /// </summary>
    [Fact]
    public async Task Unknown_instrument_version_is_not_silently_substituted()
    {
        var instrument = new ChasSgaInstrument();
        await using var services = BuildServiceProvider(instrument);

        var html = await RenderAsync(
            services,
            instrument.Definition.Code,
            instrument.Definition.CodeSystem.AbsoluteUri,
            "unknown-version");

        Assert.Contains("未找到这个量表", html, StringComparison.Ordinal);
        Assert.DoesNotContain("开始评估", html, StringComparison.Ordinal);
    }

    private static ServiceProvider BuildServiceProvider(
        INutritionAssessmentInstrument instrument)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAntDesign();
        services.AddSingleton<IJSRuntime, NoOpJsRuntime>();
        services.AddSingleton(instrument);
        services.AddScoped<NutritionAssessmentApplicationService>();
        return services.BuildServiceProvider();
    }

    private static async Task<string> RenderAsync(
        ServiceProvider services,
        string code,
        string codeSystem,
        string version)
    {
        await using var renderer = new HtmlRenderer(
            services,
            services.GetRequiredService<ILoggerFactory>());

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<AssessmentInsights>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(AssessmentInsights.Code)] = code,
                    [nameof(AssessmentInsights.CodeSystem)] = codeSystem,
                    [nameof(AssessmentInsights.Version)] = version
                }));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });
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
