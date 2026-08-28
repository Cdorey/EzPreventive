using EzNutrition.Domain.Assessments;

namespace EzNutrition.Assessments.Common.Tests;

/// <summary>
/// 验证常用量表实现的规范依据、跳题、计分和结果解释。
/// </summary>
public sealed class CommonAssessmentInstrumentTests
{
    /// <summary>
    /// 验证每个量表说明只陈述明确规范依据与临床适用信息。
    /// </summary>
    [Fact]
    public void Definitions_identify_their_exact_normative_basis()
    {
        AssertDefinition(
            new MnaSfInstrument(),
            "WS/T 888—2026",
            "附录 B.19");
        AssertDefinition(
            new MustInstrument(),
            "BAPEN MUST",
            "Steps 1–4");
        AssertDefinition(
            new SgaInstrument(),
            "国卫办医函〔2021〕552号",
            "表 2-2、表 2-3");
        AssertDefinition(
            new PgSgaInstrument(),
            "WS/T 555—2017",
            "附录 A 表 A.1 及附录 B");
        AssertDefinition(
            new WsT552ElderlyMalnutritionRiskInstrument(),
            "WS/T 552—2017",
            "附录 A 表 A.1");
    }

    /// <summary>
    /// 验证 MNA-SF 有 BMI 时直接采用快照计算第 6 项，并按 11 分阈值判定风险。
    /// </summary>
    [Fact]
    public void Mna_sf_uses_subject_bmi_and_wst_888_risk_threshold()
    {
        var instrument = new MnaSfInstrument();
        var answers = new Dictionary<string, NutritionAssessmentAnswer>(StringComparer.Ordinal)
        {
            ["food-intake-decline"] = Single("unchanged"),
            ["three-month-weight-loss"] = Single("none"),
            ["mobility"] = Single("goes-out"),
            ["psychological-stress-or-acute-disease"] = Single("no"),
            ["neuropsychological-problems"] = Single("none")
        };

        var routine = instrument.Evaluate(answers, Subject(72, 165m, 60m));
        Assert.True(routine.IsComplete);
        Assert.Equal(13m, routine.TotalScore);
        Assert.Equal("no-malnutrition-risk", routine.Interpretation?.Code);
        Assert.DoesNotContain("calf-circumference", routine.ApplicableItemCodes);

        answers["food-intake-decline"] = Single("severe");
        answers["three-month-weight-loss"] = Single("over-three-kilograms");
        answers["mobility"] = Single("bed-or-chair-bound");
        answers["psychological-stress-or-acute-disease"] = Single("yes");
        answers["neuropsychological-problems"] = Single("severe");
        var atRisk = instrument.Evaluate(answers, Subject(72, 165m, 60m));

        Assert.Equal(2m, atRisk.TotalScore);
        Assert.Equal("malnutrition-risk", atRisk.Interpretation?.Code);
    }

    /// <summary>
    /// 验证无法取得 BMI 时 MNA-SF 仅要求标准规定的小腿围替代项。
    /// </summary>
    [Fact]
    public void Mna_sf_falls_back_to_calf_circumference_when_bmi_is_unavailable()
    {
        var instrument = new MnaSfInstrument();
        var answers = new Dictionary<string, NutritionAssessmentAnswer>(StringComparer.Ordinal)
        {
            ["food-intake-decline"] = Single("unchanged"),
            ["three-month-weight-loss"] = Single("none"),
            ["mobility"] = Single("goes-out"),
            ["psychological-stress-or-acute-disease"] = Single("no"),
            ["neuropsychological-problems"] = Single("none")
        };

        var incomplete = instrument.Evaluate(answers, Subject(72));
        Assert.False(incomplete.IsComplete);
        Assert.Equal(["calf-circumference"], incomplete.MissingItemCodes);

        answers["calf-circumference"] = Single("at-least-31");
        var complete = instrument.Evaluate(answers, Subject(72));
        Assert.Equal(14m, complete.TotalScore);
    }

    /// <summary>
    /// 验证 MUST 将三步分值相加并采用 0、1、≥2 的低中高风险分级。
    /// </summary>
    [Theory]
    [InlineData("above-20", "below-five-percent", "absent", 0, "low-risk")]
    [InlineData("18-5-to-20", "below-five-percent", "absent", 1, "medium-risk")]
    [InlineData("below-18-5", "above-ten-percent", "present", 6, "high-risk")]
    public void Must_uses_the_official_three_component_risk_score(
        string bmi,
        string weightLoss,
        string acuteDisease,
        int expectedScore,
        string expectedInterpretation)
    {
        var evaluation = new MustInstrument().Evaluate(
            new Dictionary<string, NutritionAssessmentAnswer>(StringComparer.Ordinal)
            {
                ["bmi-score"] = Single(bmi),
                ["unplanned-weight-loss"] = Single(weightLoss),
                ["acute-disease-effect"] = Single(acuteDisease)
            },
            Subject(50));

        Assert.Equal(expectedScore, evaluation.TotalScore);
        Assert.Equal(expectedInterpretation, evaluation.Interpretation?.Code);
    }

    /// <summary>
    /// 验证 SGA 最终采用专业人员选择的 A/B/C 综合判断，而不是形成数值总分。
    /// </summary>
    [Fact]
    public void Sga_keeps_the_global_clinical_rating_non_numeric()
    {
        var instrument = new SgaInstrument();
        var answers = instrument.Definition.Items.ToDictionary(
            item => item.Code,
            item => (NutritionAssessmentAnswer)Single(
                item.Code == "global-rating" ? "b" : "a"),
            StringComparer.Ordinal);

        var evaluation = instrument.Evaluate(answers, Subject(70));

        Assert.True(evaluation.IsComplete);
        Assert.Null(evaluation.TotalScore);
        Assert.Empty(evaluation.Metrics);
        Assert.Equal("mild-to-moderate-malnutrition", evaluation.Interpretation?.Code);
        Assert.All(
            instrument.Definition.Items.SelectMany(item => item.Options),
            option => Assert.Null(option.Score));
    }

    /// <summary>
    /// 验证 PG-SGA 依据 1 个月体重下降率、症状累计分及 A/B/C/D 分项形成总分。
    /// </summary>
    [Fact]
    public void Pg_sga_recalculates_weight_loss_and_accumulates_multi_select_scores()
    {
        var instrument = new PgSgaInstrument();
        var answers = new Dictionary<string, NutritionAssessmentAnswer>(StringComparer.Ordinal)
        {
            ["weight-reference"] = Single("one-month"),
            ["reference-weight"] = Number(50m),
            ["two-week-weight-trend"] = Single("decreased"),
            ["one-month-intake-change"] = Single("less"),
            ["current-intake"] = Single("normal-but-less"),
            ["nutrition-impact-symptoms"] = Multiple("nausea", "vomiting"),
            ["activity-and-function"] = Single("slightly-worse"),
            ["comorbidities"] = Multiple("cancer"),
            ["fever"] = Single("moderate"),
            ["fever-duration"] = Single("above-72-hours"),
            ["fever-related-steroid-dose"] = Single("10-to-30"),
            ["overall-muscle-loss"] = Single("moderate")
        };

        var evaluation = instrument.Evaluate(answers, Subject(66, 170m, 46m));

        Assert.True(evaluation.IsComplete);
        Assert.Equal(21m, evaluation.TotalScore);
        Assert.Equal("severe-malnutrition", evaluation.Interpretation?.Code);
        Assert.Equal(
            8m,
            Assert.Single(
                evaluation.Metrics,
                metric => metric.Code == "weight-loss-percentage").Value);
        Assert.Equal(
            10m,
            Assert.Single(evaluation.Metrics, metric => metric.Code == "patient-score").Value);
        Assert.Equal(
            7m,
            Assert.Single(evaluation.Metrics, metric => metric.Code == "stress-score").Value);
    }

    /// <summary>
    /// 验证 PG-SGA 只在所选体重资料和发热路径上显示、要求对应题目。
    /// </summary>
    [Fact]
    public void Pg_sga_exposes_only_the_selected_conditional_items()
    {
        var instrument = new PgSgaInstrument();
        var answers = new Dictionary<string, NutritionAssessmentAnswer>(StringComparer.Ordinal)
        {
            ["weight-reference"] = Single("subjective"),
            ["fever"] = Single("none")
        };

        var evaluation = instrument.Evaluate(answers, Subject(60, 170m, 70m));

        Assert.Contains("subjective-weight-loss", evaluation.ApplicableItemCodes);
        Assert.DoesNotContain("reference-weight", evaluation.ApplicableItemCodes);
        Assert.DoesNotContain("fever-duration", evaluation.ApplicableItemCodes);
        Assert.DoesNotContain("fever-related-steroid-dose", evaluation.ApplicableItemCodes);
    }

    /// <summary>
    /// 验证 WS/T 552 初筛达到 12 分后结束，不创建后续评估的必答状态。
    /// </summary>
    [Fact]
    public void Wst_552_stops_after_a_negative_screening()
    {
        var instrument = new WsT552ElderlyMalnutritionRiskInstrument();
        var evaluation = instrument.Evaluate(
            Wst552ScreeningAnswers(
                bmi: "23-to-below-24",
                weight: "below-one-kilogram",
                mobility: "independent-outdoors",
                dental: "normal",
                neuropsychological: "none",
                intake: "unchanged"),
            Subject(69));

        Assert.True(evaluation.IsComplete);
        Assert.Equal(14m, evaluation.TotalScore);
        Assert.Equal("no-malnutrition-risk", evaluation.Interpretation?.Code);
        Assert.Equal(6, evaluation.ApplicableItemCodes.Count);
    }

    /// <summary>
    /// 验证 WS/T 552 初筛阳性后继续评估，并保留半分项与年龄调整。
    /// </summary>
    [Fact]
    public void Wst_552_continues_after_positive_screening_and_applies_age_adjustment()
    {
        var instrument = new WsT552ElderlyMalnutritionRiskInstrument();
        var answers = Wst552ScreeningAnswers(
            bmi: "below-19",
            weight: "over-three-kilograms",
            mobility: "bedridden",
            dental: "full-or-half-missing",
            neuropsychological: "severe",
            intake: "severe");
        AddWst552AssessmentAnswers(answers);

        var evaluation = instrument.Evaluate(answers, Subject(70));

        Assert.True(evaluation.IsComplete);
        Assert.Equal(17m, evaluation.TotalScore);
        Assert.Equal("malnutrition", evaluation.Interpretation?.Code);
        Assert.Equal(
            16m,
            Assert.Single(evaluation.Metrics, metric => metric.Code == "assessment-score").Value);
        Assert.Equal(
            1m,
            Assert.Single(evaluation.Metrics, metric => metric.Code == "age-score").Value);
    }

    private static void AssertDefinition(
        INutritionAssessmentInstrument instrument,
        string version,
        string basisText)
    {
        Assert.Equal(version, instrument.Definition.Version);
        Assert.Contains(basisText, instrument.Definition.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("授权", instrument.Definition.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("免责声明", instrument.Definition.Description, StringComparison.Ordinal);
    }

    private static Dictionary<string, NutritionAssessmentAnswer> Wst552ScreeningAnswers(
        string bmi,
        string weight,
        string mobility,
        string dental,
        string neuropsychological,
        string intake) => new(StringComparer.Ordinal)
        {
            ["bmi"] = Single(bmi),
            ["three-month-weight-change"] = Single(weight),
            ["mobility"] = Single(mobility),
            ["dental-status"] = Single(dental),
            ["neuropsychological-disorder"] = Single(neuropsychological),
            ["three-month-intake-change"] = Single(intake)
        };

    private static void AddWst552AssessmentAnswers(
        IDictionary<string, NutritionAssessmentAnswer> answers)
    {
        answers["more-than-three-chronic-diseases"] = Single("no");
        answers["more-than-three-long-term-medications"] = Single("no");
        answers["living-alone"] = Single("no");
        answers["sleep-duration"] = Single("at-least-five-hours");
        answers["independent-outdoor-activity"] = Single("at-least-one-hour");
        answers["education"] = Single("middle-or-above");
        answers["perceived-economic-status"] = Single("good");
        answers["feeding-ability"] = Single("independent");
        answers["daily-meals"] = Single("three-or-more");
        answers["daily-protein-food-groups"] = Single("three");
        answers["daily-cooking-oil"] = Single("at-most-25-grams");
        answers["daily-fruit-and-vegetables"] = Single("yes");
        answers["calf-circumference"] = Single("at-least-31");
        answers["waist-circumference"] = Single("within-threshold");
    }

    private static NutritionAssessmentSingleChoiceAnswer Single(string optionCode) =>
        new(optionCode);

    private static NutritionAssessmentMultipleChoiceAnswer Multiple(
        params string[] optionCodes) => new(optionCodes);

    private static NutritionAssessmentDecimalAnswer Number(decimal value) => new(value);

    private static NutritionAssessmentSubject Subject(
        int age,
        decimal? height = null,
        decimal? weight = null) => new()
        {
            AgeInYears = age,
            HeightInCentimeters = height,
            WeightInKilograms = weight
        };
}
