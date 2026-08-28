using System.Net;
using AntDesign;
using EzNutrition.Application.Consultations;
using EzNutrition.Assessments.Common;
using EzNutrition.Domain.Assessments;
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
public sealed class NutritionAssessmentScaleRenderTests
{
    /// <summary>
    /// 验证初始状态显示标准规定的全部评分项目，但不显示 SOAP 引入入口。
    /// </summary>
    [Fact]
    public async Task Initial_state_renders_all_standard_scoring_items()
    {
        var workspace = CreateWorkspace();

        var html = await RenderAsync(workspace);

        Assert.Contains("临床营养风险筛查 NRS 2002", html, StringComparison.Ordinal);
        Assert.Contains(
            "本量表依据 WS/T 427—2013《临床营养风险筛查》",
            html,
            StringComparison.Ordinal);
        Assert.Contains("体质指数（BMI）与一般临床状况", html, StringComparison.Ordinal);
        Assert.Contains("近期（1 个月～3 个月）体重是否下降", html, StringComparison.Ordinal);
        Assert.Contains("近一周进食量是否减少", html, StringComparison.Ordinal);
        Assert.Contains("疾病严重程度", html, StringComparison.Ordinal);
        Assert.DoesNotContain("引入 SOAP", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证四项评分完整后显示标准判定并提供 SOAP 复核入口。
    /// </summary>
    [Fact]
    public async Task Completed_screening_renders_result_and_soap_import()
    {
        var workspace = CreateWorkspace();
        var run = Assert.Single(workspace.NutritionAssessments);
        run.SetAnswer("bmi-status", "bmi-at-least-18-5");
        run.SetAnswer("recent-weight-loss", "over-five-percent-within-three-months");
        run.SetAnswer("last-week-intake-reduction", "no-scored-intake-reduction");

        var beforeCompletion = await RenderAsync(workspace);
        Assert.Contains("尚有 1 项必答内容未完成", beforeCompletion, StringComparison.Ordinal);
        Assert.DoesNotContain("引入 SOAP", beforeCompletion, StringComparison.Ordinal);

        run.SetAnswer("disease-severity", "mild");
        var completed = await RenderAsync(workspace);

        Assert.Contains("有营养风险", completed, StringComparison.Ordinal);
        Assert.Contains("总分 3", completed, StringComparison.Ordinal);
        Assert.Contains("引入 SOAP", completed, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证结果颜色由通用关注程度决定，而不解释任何具体量表的结果编码。
    /// </summary>
    [Theory]
    [InlineData("arbitrary-routine-code", NutritionAssessmentAttentionLevel.Routine, "green")]
    [InlineData(
        "arbitrary-attention-code",
        NutritionAssessmentAttentionLevel.RequiresAttention,
        "red")]
    [InlineData("nutritional-risk", NutritionAssessmentAttentionLevel.Routine, "green")]
    [InlineData("arbitrary-code", NutritionAssessmentAttentionLevel.Unspecified, "blue")]
    public void Result_color_uses_attention_level_instead_of_interpretation_code(
        string interpretationCode,
        NutritionAssessmentAttentionLevel attentionLevel,
        string expectedColor)
    {
        var evaluation = new NutritionAssessmentEvaluation
        {
            IsComplete = true,
            ApplicableItemCodes = new HashSet<string>(StringComparer.Ordinal),
            MissingItemCodes = [],
            Interpretation = new NutritionAssessmentInterpretation(
                interpretationCode,
                "测试结果",
                attentionLevel)
        };

        Assert.Equal(expectedColor, NutritionAssessmentScale.ResultColor(evaluation));
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
        var service = new NutritionAssessmentApplicationService([new Nrs2002Instrument()]);
        service.StartRun(
            workspace,
            Assert.Single(service.Definitions),
            workspace.ContractIdentity.CreatedAt);
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
            var output = await renderer.RenderComponentAsync<NutritionAssessmentScale>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(NutritionAssessmentScale.Assessment)] =
                        Assert.Single(workspace.NutritionAssessments),
                    [nameof(NutritionAssessmentScale.OnSoapContributionConfirmed)] =
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
