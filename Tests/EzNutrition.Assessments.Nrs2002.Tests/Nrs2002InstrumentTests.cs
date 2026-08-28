using EzNutrition.Assessments.Nrs2002;
using EzNutrition.Domain.Assessments;

namespace EzNutrition.Assessments.Nrs2002.Tests;

/// <summary>
/// 验证 NRS 2002 实现与 WS/T 427—2013 的题目、计分和结果判定一致。
/// </summary>
public sealed class Nrs2002InstrumentTests
{
    private readonly Nrs2002Instrument instrument = new();

    /// <summary>
    /// 验证具体量表库只依赖 Domain，不获取 UI、Application、档案或宿主职责。
    /// </summary>
    [Fact]
    public void Assembly_only_depends_on_domain_layer()
    {
        var references = typeof(Nrs2002Instrument).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.Contains("EzNutrition.Domain", references);
        Assert.DoesNotContain("EzNutrition.Application", references);
        Assert.DoesNotContain("EzNutrition.Archives.Contracts", references);
        Assert.DoesNotContain("EzNutrition.UI", references);
        Assert.DoesNotContain("EzNutrition.Presentation", references);
        Assert.DoesNotContain("EzNutrition.Client", references);
        Assert.DoesNotContain("AntDesign", references);
    }

    /// <summary>
    /// 验证量表明确标识采用的卫生行业标准，并仅以说明提示适用对象。
    /// </summary>
    [Fact]
    public void Definition_identifies_wst_427_and_describes_the_intended_population()
    {
        Assert.Equal("WS/T 427—2013", instrument.Definition.Version);
        Assert.Contains("chinacdc.cn", instrument.Definition.DefinitionUri.Host, StringComparison.Ordinal);
        Assert.Contains(
            "本量表依据 WS/T 427—2013《临床营养风险筛查》",
            instrument.Definition.Description,
            StringComparison.Ordinal);
        Assert.Contains("年龄 18 岁～90 岁", instrument.Definition.Description, StringComparison.Ordinal);
        Assert.Contains("入院后 24 h 内", instrument.Definition.Description, StringComparison.Ordinal);

        var pediatricEvaluation = instrument.Evaluate(StandardAnswers(), Subject(17));
        Assert.True(pediatricEvaluation.IsComplete);
    }

    /// <summary>
    /// 验证营养状况受损评分采用标准规定的 BMI、体重和进食量三个小结。
    /// </summary>
    [Fact]
    public void Definition_uses_wst_427_nutritional_status_categories()
    {
        var bmi = Item("bmi-status");
        Assert.Contains(
            bmi.Options,
            option => option.Score == 3m
                && option.Display.Contains(
                    "BMI < 18.5 kg/m²，伴一般临床状况差",
                    StringComparison.Ordinal));

        var weightLoss = Item("recent-weight-loss");
        Assert.Collection(
            weightLoss.Options,
            option => AssertOption(option, 0m, "未达到下列 1～3 分条件"),
            option => AssertOption(option, 1m, "近 3 个月内体重下降 >5%"),
            option => AssertOption(option, 2m, "近 2 个月内体重下降 >5%"),
            option => AssertOption(option, 3m, "近 1 个月内体重下降 >5%"));

        var intakeReduction = Item("last-week-intake-reduction");
        Assert.Collection(
            intakeReduction.Options,
            option => AssertOption(option, 0m, "较从前减少不足 25%"),
            option => AssertOption(option, 1m, "较从前减少 25%～50%"),
            option => AssertOption(option, 2m, "较从前减少 51%～75%"),
            option => AssertOption(option, 3m, "较从前减少 76% 及以上"));
    }

    /// <summary>
    /// 验证标准列出的疾病分级示例完整呈现在题目中。
    /// </summary>
    [Fact]
    public void Definition_uses_wst_427_disease_severity_categories()
    {
        var options = Item("disease-severity").Options;

        Assert.Contains(
            options,
            option => option.Score == 1m
                && option.Display.Contains("慢性阻塞性肺病", StringComparison.Ordinal)
                && option.Display.Contains("肝硬化", StringComparison.Ordinal));
        Assert.Contains(
            options,
            option => option.Score == 2m
                && option.Display.Contains("腹部大手术", StringComparison.Ordinal)
                && option.Display.Contains("血液恶性肿瘤", StringComparison.Ordinal));
        Assert.Contains(
            options,
            option => option.Score == 3m
                && option.Display.Contains("APACHE-II 评分 >10 分", StringComparison.Ordinal));
    }

    /// <summary>
    /// 验证量表不再采用 WS/T 427—2013 中不存在的四问初筛分支。
    /// </summary>
    [Fact]
    public void Screening_requires_all_four_standard_scored_items()
    {
        var evaluation = instrument.Evaluate(
            new Dictionary<string, string>(StringComparer.Ordinal),
            Subject(69));

        Assert.False(evaluation.IsComplete);
        Assert.Equal(4, evaluation.ApplicableItemCodes.Count);
        Assert.Equal(
            ["bmi-status", "recent-weight-loss", "last-week-intake-reduction", "disease-severity"],
            evaluation.MissingItemCodes);
        Assert.Null(evaluation.TotalScore);
    }

    /// <summary>
    /// 验证 BMI、体重和进食量小结取最高值，而不是相加。
    /// </summary>
    [Fact]
    public void Nutritional_status_score_is_the_highest_of_three_subscores()
    {
        var evaluation = instrument.Evaluate(
            StandardAnswers(
                weightLoss: "over-five-percent-within-two-months",
                intakeReduction: "reduced-25-to-50-percent"),
            Subject(69));

        Assert.Equal(2m, evaluation.TotalScore);
        Assert.Equal(
            2m,
            Assert.Single(
                evaluation.Metrics,
                metric => metric.Code == "impaired-nutritional-status-score").Value);
    }

    /// <summary>
    /// 验证达到 70 岁时加 1 分，并可使总分跨过 3 分风险阈值。
    /// </summary>
    [Theory]
    [InlineData(69, 2, "no-current-nutritional-risk")]
    [InlineData(70, 3, "nutritional-risk")]
    public void Age_score_applies_from_seventy(
        int ageInYears,
        int expectedTotal,
        string expectedInterpretation)
    {
        var answers = StandardAnswers(
            weightLoss: "over-five-percent-within-three-months",
            diseaseSeverity: "mild");

        var evaluation = instrument.Evaluate(answers, Subject(ageInYears));

        Assert.True(evaluation.IsComplete);
        Assert.Equal(expectedTotal, evaluation.TotalScore);
        Assert.Equal(expectedInterpretation, evaluation.Interpretation?.Code);
        Assert.Equal(
            ageInYears >= 70 ? 1m : 0m,
            Assert.Single(evaluation.Metrics, metric => metric.Code == "age-score").Value);
    }

    /// <summary>
    /// 验证营养状况受损、疾病严重程度与年龄三部分相加。
    /// </summary>
    [Fact]
    public void Total_score_sums_nutritional_disease_and_age_components()
    {
        var answers = StandardAnswers(
            bmiStatus: "bmi-below-18-5-with-poor-general-condition",
            weightLoss: "over-five-percent-within-two-months",
            intakeReduction: "reduced-25-to-50-percent",
            diseaseSeverity: "moderate");

        var evaluation = instrument.Evaluate(answers, Subject(78));

        Assert.Equal(6m, evaluation.TotalScore);
        Assert.Equal("nutritional-risk", evaluation.Interpretation?.Code);
        Assert.Equal(6, evaluation.Metrics.Count);
        Assert.Contains("总分 6 分", evaluation.SoapContribution.Objective, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证达到风险阈值时采用标准规定的判定和处理文本。
    /// </summary>
    [Fact]
    public void At_risk_result_uses_the_standard_interpretation_and_plan()
    {
        var evaluation = instrument.Evaluate(
            StandardAnswers(
                bmiStatus: "bmi-below-18-5-with-poor-general-condition"),
            Subject(69));

        Assert.Equal("有营养风险", evaluation.Interpretation?.Display);
        Assert.Equal(
            NutritionAssessmentAttentionLevel.RequiresAttention,
            evaluation.Interpretation?.AttentionLevel);
        Assert.Equal(
            "应结合患者的临床状况，制定营养支持治疗计划。",
            evaluation.SoapContribution.Plan);
    }

    /// <summary>
    /// 验证低于风险阈值时采用标准规定的判定和每周复筛要求。
    /// </summary>
    [Fact]
    public void Below_threshold_result_uses_the_standard_interpretation_and_plan()
    {
        var evaluation = instrument.Evaluate(StandardAnswers(), Subject(69));

        Assert.Equal("目前没有营养风险", evaluation.Interpretation?.Display);
        Assert.Equal(
            NutritionAssessmentAttentionLevel.Routine,
            evaluation.Interpretation?.AttentionLevel);
        Assert.Equal("应每周重复进行筛查。", evaluation.SoapContribution.Plan);
    }

    /// <summary>
    /// 验证量表实现不会把未知的题目或选项静默解释成临床结果。
    /// </summary>
    [Fact]
    public void Unknown_answer_is_rejected()
    {
        var answers = StandardAnswers();
        answers["disease-severity"] = "unknown";

        Assert.Throws<ArgumentException>(() => instrument.Evaluate(answers, Subject(69)));
    }

    private NutritionAssessmentItem Item(string code) =>
        instrument.Definition.Items.Single(item => item.Code == code);

    private static void AssertOption(
        NutritionAssessmentOption option,
        decimal score,
        string text)
    {
        Assert.Equal(score, option.Score);
        Assert.Contains(text, option.Display, StringComparison.Ordinal);
    }

    private static Dictionary<string, string> StandardAnswers(
        string bmiStatus = "bmi-at-least-18-5",
        string weightLoss = "no-scored-weight-loss",
        string intakeReduction = "no-scored-intake-reduction",
        string diseaseSeverity = "no-scored-disease-severity") =>
        new(StringComparer.Ordinal)
        {
            ["bmi-status"] = bmiStatus,
            ["recent-weight-loss"] = weightLoss,
            ["last-week-intake-reduction"] = intakeReduction,
            ["disease-severity"] = diseaseSeverity
        };

    private static NutritionAssessmentSubject Subject(int ageInYears) => new()
    {
        AgeInYears = ageInYears,
        HeightInCentimeters = 165m,
        WeightInKilograms = 60m
    };
}
