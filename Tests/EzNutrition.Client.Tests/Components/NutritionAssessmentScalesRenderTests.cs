using System.Net;
using AntDesign;
using EzNutrition.Application.Consultations;
using EzNutrition.Assessments.Nrs2002;
using EzNutrition.Domain.Consultations;
using EzNutrition.UI.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace EzNutrition.Client.Tests.Components;

/// <summary>
/// 验证通用量表组件按领域评估状态显示题目与 SOAP 复核入口。
/// </summary>
public sealed class NutritionAssessmentScalesRenderTests
{
    /// <summary>
    /// 验证尚未完成初筛时不提前显示终筛项目或 SOAP 引入入口。
    /// </summary>
    [Fact]
    public async Task Initial_state_only_renders_applicable_screening_items()
    {
        var workspace = CreateWorkspace();

        var html = await RenderAsync(workspace);

        Assert.Contains("营养风险筛查 NRS 2002", html, StringComparison.Ordinal);
        Assert.Contains("BMI 是否低于 20.5", html, StringComparison.Ordinal);
        Assert.DoesNotContain("营养状态受损程度", html, StringComparison.Ordinal);
        Assert.DoesNotContain("引入 SOAP", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证初筛阳性后显示终筛，完整计分后才提供 SOAP 复核入口。
    /// </summary>
    [Fact]
    public async Task Positive_path_renders_final_items_and_completed_result()
    {
        var workspace = CreateWorkspace();
        var run = Assert.Single(workspace.NutritionAssessments);
        run.SetAnswer("initial-bmi-below-20-5", "yes");
        run.SetAnswer("initial-weight-loss-within-three-months", "no");
        run.SetAnswer("initial-reduced-intake-last-week", "no");
        run.SetAnswer("initial-severe-illness", "no");

        var beforeCompletion = await RenderAsync(workspace);
        Assert.Contains("营养状态受损程度", beforeCompletion, StringComparison.Ordinal);
        Assert.DoesNotContain("引入 SOAP", beforeCompletion, StringComparison.Ordinal);

        run.SetAnswer("impaired-nutritional-status", "1");
        run.SetAnswer("disease-severity", "1");
        var completed = await RenderAsync(workspace);

        Assert.Contains("存在营养风险", completed, StringComparison.Ordinal);
        Assert.Contains("总分 3", completed, StringComparison.Ordinal);
        Assert.Contains("引入 SOAP", completed, StringComparison.Ordinal);
    }

    private static ConsultationWorkspace CreateWorkspace()
    {
        var workspace = new ConsultationWorkspace(new ClientInfo
        {
            Gender = "女",
            Age = new ChronologicalAge(70),
            Height = 165m,
            Weight = 60m
        });
        new NutritionAssessmentApplicationService([new Nrs2002Instrument()])
            .EnsureRuns(workspace, workspace.ContractIdentity.CreatedAt);
        return workspace;
    }

    private static async Task<string> RenderAsync(ConsultationWorkspace workspace)
    {
        await using var services = new ServiceCollection()
            .AddLogging()
            .AddAntDesign()
            .AddSingleton<IJSRuntime, NoOpJsRuntime>()
            .BuildServiceProvider();
        await using var renderer = new HtmlRenderer(
            services,
            services.GetRequiredService<ILoggerFactory>());

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<NutritionAssessmentScales>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(NutritionAssessmentScales.Assessments)] = workspace.NutritionAssessments,
                    [nameof(NutritionAssessmentScales.OnSoapContributionConfirmed)] =
                        EventCallback.Factory.Create<SoapContribution>(new object(), _ => { })
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
