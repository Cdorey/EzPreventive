using EzNutrition.Assessments.Nrs2002;
using EzNutrition.Domain.Assessments;

namespace EzNutrition.Assessments.Nrs2002.Tests;

/// <summary>
/// 验证 NRS 2002 的正式分支、年龄校正和风险阈值。
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
    /// 验证适用范围以问卷说明呈现，不被实现为阻止专业人员使用的硬约束。
    /// </summary>
    [Fact]
    public void Definition_describes_the_intended_population()
    {
        Assert.Contains(
            "适用范围：成年住院患者",
            instrument.Definition.Description,
            StringComparison.Ordinal);

        var pediatricEvaluation = instrument.Evaluate(InitialAnswers(), Subject(17));
        Assert.True(pediatricEvaluation.IsComplete);
    }

    /// <summary>
    /// 验证四项初筛均为否时结束筛查，不虚构终筛总分。
    /// </summary>
    [Fact]
    public void All_negative_initial_answers_complete_without_final_score()
    {
        var evaluation = instrument.Evaluate(
            InitialAnswers(),
            Subject(69));

        Assert.True(evaluation.IsComplete);
        Assert.Equal(4, evaluation.ApplicableItemCodes.Count);
        Assert.Empty(evaluation.MissingItemCodes);
        Assert.Null(evaluation.TotalScore);
        Assert.Equal("negative-initial-screening", evaluation.Interpretation?.Code);
        Assert.Contains("每周复筛", evaluation.SoapContribution.Plan, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证任一初筛项为是时必须继续回答两个终筛项目。
    /// </summary>
    [Fact]
    public void Positive_initial_screen_requires_both_final_items()
    {
        var answers = InitialAnswers(bmiBelow205: true);

        var evaluation = instrument.Evaluate(answers, Subject(69));

        Assert.False(evaluation.IsComplete);
        Assert.Equal(6, evaluation.ApplicableItemCodes.Count);
        Assert.Equal(
            ["impaired-nutritional-status", "disease-severity"],
            evaluation.MissingItemCodes);
    }

    /// <summary>
    /// 验证达到 70 岁时加 1 分，并可使总分跨过 3 分风险阈值。
    /// </summary>
    [Theory]
    [InlineData(69, 2, "below-risk-threshold")]
    [InlineData(70, 3, "nutritional-risk")]
    public void Age_adjustment_applies_from_seventy(
        int ageInYears,
        int expectedTotal,
        string expectedInterpretation)
    {
        var answers = InitialAnswers(bmiBelow205: true);
        answers["impaired-nutritional-status"] = "1";
        answers["disease-severity"] = "1";

        var evaluation = instrument.Evaluate(answers, Subject(ageInYears));

        Assert.True(evaluation.IsComplete);
        Assert.Equal(expectedTotal, evaluation.TotalScore);
        Assert.Equal(expectedInterpretation, evaluation.Interpretation?.Code);
        Assert.Equal(
            ageInYears >= 70 ? 1m : 0m,
            Assert.Single(
                evaluation.Metrics,
                metric => metric.Code == "age-adjustment-score").Value);
    }

    /// <summary>
    /// 验证终筛两部分与年龄校正按确定性规则相加。
    /// </summary>
    [Fact]
    public void Final_score_sums_nutritional_disease_and_age_components()
    {
        var answers = InitialAnswers(reducedIntake: true);
        answers["impaired-nutritional-status"] = "3";
        answers["disease-severity"] = "2";

        var evaluation = instrument.Evaluate(answers, Subject(78));

        Assert.Equal(6m, evaluation.TotalScore);
        Assert.Equal("nutritional-risk", evaluation.Interpretation?.Code);
        Assert.Equal(3, evaluation.Metrics.Count);
        Assert.Contains("总分 6 分", evaluation.SoapContribution.Objective, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证量表实现不会把未知的题目或选项静默解释成临床结果。
    /// </summary>
    [Fact]
    public void Unknown_answer_is_rejected()
    {
        var answers = InitialAnswers();
        answers["initial-severe-illness"] = "unknown";

        Assert.Throws<ArgumentException>(() => instrument.Evaluate(answers, Subject(69)));
    }

    private static Dictionary<string, string> InitialAnswers(
        bool bmiBelow205 = false,
        bool weightLoss = false,
        bool reducedIntake = false,
        bool severeIllness = false) => new(StringComparer.Ordinal)
        {
            ["initial-bmi-below-20-5"] = YesNo(bmiBelow205),
            ["initial-weight-loss-within-three-months"] = YesNo(weightLoss),
            ["initial-reduced-intake-last-week"] = YesNo(reducedIntake),
            ["initial-severe-illness"] = YesNo(severeIllness)
        };

    private static NutritionAssessmentSubject Subject(int ageInYears) => new()
    {
        AgeInYears = ageInYears,
        HeightInCentimeters = 165m,
        WeightInKilograms = 60m
    };

    private static string YesNo(bool value) => value ? "yes" : "no";
}
