using System.Globalization;
using EzNutrition.Domain.Assessments;
using EzNutrition.Domain.Consultations;

namespace EzNutrition.Assessments.Nrs2002;

/// <summary>
/// 按 WS/T 427—2013 实现成年住院患者临床营养风险筛查的题目、计分和结果判定。
/// </summary>
/// <remarks>
/// 本标准采用营养风险筛查 2002（NRS 2002）方法。适用范围和评分规则依据
/// WS/T 427—2013《临床营养风险筛查》及其资料性附录 A。
/// </remarks>
public sealed class Nrs2002Instrument : INutritionAssessmentInstrument
{
    private const string BmiStatusCode = "bmi-status";
    private const string WeightLossCode = "recent-weight-loss";
    private const string IntakeReductionCode = "last-week-intake-reduction";
    private const string DiseaseSeverityCode = "disease-severity";

    private static readonly string[] ItemCodes =
    [
        BmiStatusCode,
        WeightLossCode,
        IntakeReductionCode,
        DiseaseSeverityCode
    ];

    private static readonly IReadOnlySet<string> ApplicableItemCodes =
        new HashSet<string>(ItemCodes, StringComparer.Ordinal);

    private static readonly NutritionAssessmentDefinition InstrumentDefinition = new()
    {
        CodeSystem = new Uri("https://eznutrition.cdorey.net/codes/nutrition-assessment"),
        Code = "nrs-2002",
        Version = "WS/T 427—2013",
        DefinitionUri = new Uri(
            "https://www.chinacdc.cn/jkyj/yyyjk2/jswj13949/201707/P020240905519500515861.pdf"),
        DisplayName = "临床营养风险筛查 NRS 2002",
        Description =
            "本量表依据 WS/T 427—2013《临床营养风险筛查》。适用范围：年龄 18 岁～90 岁、住院过夜、入院次日 8 时前未进行急诊手术、神志清楚且愿意接受筛查的成年住院患者。应在入院后 24 h 内完成筛查。",
        Sections =
        [
            new NutritionAssessmentSection(
                "impaired-nutritional-status",
                "营养状况受损评分",
                [
                    new NutritionAssessmentItem(
                        BmiStatusCode,
                        "体质指数（BMI）与一般临床状况",
                        [
                            ScoredOption(
                                "bmi-at-least-18-5",
                                0,
                                "BMI ≥ 18.5 kg/m²"),
                            ScoredOption(
                                "bmi-below-18-5-without-poor-general-condition",
                                0,
                                "BMI < 18.5 kg/m²，但不伴一般临床状况差"),
                            ScoredOption(
                                "bmi-below-18-5-with-poor-general-condition",
                                3,
                                "BMI < 18.5 kg/m²，伴一般临床状况差")
                        ],
                        "WS/T 427—2013 规定：BMI ≥ 18.5 kg/m² 计 0 分；BMI < 18.5 kg/m² 且伴一般临床状况差计 3 分。"),
                    new NutritionAssessmentItem(
                        WeightLossCode,
                        "近期（1 个月～3 个月）体重是否下降？",
                        [
                            ScoredOption(
                                "no-scored-weight-loss",
                                0,
                                "体重无下降，或未达到下列 1～3 分条件"),
                            ScoredOption(
                                "over-five-percent-within-three-months",
                                1,
                                "近 3 个月内体重下降 >5%"),
                            ScoredOption(
                                "over-five-percent-within-two-months",
                                2,
                                "近 2 个月内体重下降 >5%"),
                            ScoredOption(
                                "severe-weight-loss",
                                3,
                                "近 1 个月内体重下降 >5%，或近 3 个月内体重下降 >15%")
                        ],
                        "符合多个条件时取最高评分。"),
                    new NutritionAssessmentItem(
                        IntakeReductionCode,
                        "近一周进食量是否减少？",
                        [
                            ScoredOption(
                                "no-scored-intake-reduction",
                                0,
                                "进食量无变化，或较从前减少不足 25%"),
                            ScoredOption(
                                "reduced-25-to-50-percent",
                                1,
                                "较从前减少 25%～50%"),
                            ScoredOption(
                                "reduced-51-to-75-percent",
                                2,
                                "较从前减少 51%～75%"),
                            ScoredOption(
                                "reduced-at-least-76-percent",
                                3,
                                "较从前减少 76% 及以上")
                        ])
                ],
                "取 BMI、体重状况和进食状况三个小结评分中的最高值，作为营养状况受损评分。"),
            new NutritionAssessmentSection(
                "disease-and-age",
                "疾病严重程度与年龄评分",
                [
                    new NutritionAssessmentItem(
                        DiseaseSeverityCode,
                        "疾病严重程度",
                        [
                            ScoredOption(
                                "no-scored-disease-severity",
                                0,
                                "未达到下列 1～3 分疾病严重程度"),
                            ScoredOption(
                                "mild",
                                1,
                                "髋骨折、慢性疾病急性发作或者并发症、慢性阻塞性肺病、血液透析、肝硬化、一般恶性肿瘤、糖尿病"),
                            ScoredOption(
                                "moderate",
                                2,
                                "腹部大手术、脑卒中、重度肺炎、血液恶性肿瘤"),
                            ScoredOption(
                                "severe",
                                3,
                                "颅脑损伤、骨髓移植、APACHE-II 评分 >10 分的 ICU 患者")
                        ],
                        "未列入的疾病应由受过培训的实施人员参照上述疾病严重程度评分，复核者有权决定参照位置。")
                ],
                "疾病严重程度计 0～3 分；年龄 ≥70 岁计 1 分，否则计 0 分。筛查总分为营养状况受损评分、疾病严重程度评分与年龄评分之和。")
        ]
    };

    /// <inheritdoc />
    public NutritionAssessmentDefinition Definition => InstrumentDefinition;

    /// <inheritdoc />
    public NutritionAssessmentEvaluation Evaluate(
        IReadOnlyDictionary<string, string> answers,
        NutritionAssessmentSubject subject)
    {
        ArgumentNullException.ThrowIfNull(answers);
        ArgumentNullException.ThrowIfNull(subject);
        ValidateAnswers(answers);

        var missingItems = ItemCodes
            .Where(code => !answers.ContainsKey(code))
            .ToArray();
        if (missingItems.Length > 0)
        {
            return Incomplete(missingItems);
        }

        var bmiScore = Score(BmiStatusCode, answers[BmiStatusCode]);
        var weightLossScore = Score(WeightLossCode, answers[WeightLossCode]);
        var intakeReductionScore = Score(IntakeReductionCode, answers[IntakeReductionCode]);
        var nutritionalStatusScore = new[]
        {
            bmiScore,
            weightLossScore,
            intakeReductionScore
        }.Max();
        var diseaseSeverityScore = Score(
            DiseaseSeverityCode,
            answers[DiseaseSeverityCode]);
        var ageScore = subject.AgeInYears >= 70 ? 1m : 0m;
        var totalScore = nutritionalStatusScore + diseaseSeverityScore + ageScore;
        var atRisk = totalScore >= 3m;
        var totalText = totalScore.ToString(CultureInfo.InvariantCulture);
        var interpretation = atRisk
            ? new NutritionAssessmentInterpretation(
                "nutritional-risk",
                "有营养风险",
                NutritionAssessmentAttentionLevel.RequiresAttention)
            : new NutritionAssessmentInterpretation(
                "no-current-nutritional-risk",
                "目前没有营养风险",
                NutritionAssessmentAttentionLevel.Routine);

        return new NutritionAssessmentEvaluation
        {
            IsComplete = true,
            ApplicableItemCodes = ApplicableItemCodes,
            MissingItemCodes = [],
            TotalScore = totalScore,
            Metrics =
            [
                new NutritionAssessmentMetric("bmi-score", "BMI 评分", bmiScore),
                new NutritionAssessmentMetric(
                    "weight-loss-score",
                    "体重状况评分",
                    weightLossScore),
                new NutritionAssessmentMetric(
                    "intake-reduction-score",
                    "进食状况评分",
                    intakeReductionScore),
                new NutritionAssessmentMetric(
                    "impaired-nutritional-status-score",
                    "营养状况受损评分",
                    nutritionalStatusScore),
                new NutritionAssessmentMetric(
                    "disease-severity-score",
                    "疾病严重程度评分",
                    diseaseSeverityScore),
                new NutritionAssessmentMetric("age-score", "年龄评分", ageScore)
            ],
            Interpretation = interpretation,
            SoapContribution = new SoapContribution
            {
                Objective =
                    $"临床营养风险筛查（WS/T 427—2013）：BMI {bmiScore:0} 分，体重状况 {weightLossScore:0} 分，进食状况 {intakeReductionScore:0} 分；营养状况受损评分取三项最高值 {nutritionalStatusScore:0} 分，疾病严重程度 {diseaseSeverityScore:0} 分，年龄 {ageScore:0} 分，总分 {totalText} 分。",
                Assessment =
                    $"临床营养风险筛查结果：{interpretation.Display}（总分 {totalText} 分）。",
                Plan = atRisk
                    ? "应结合患者的临床状况，制定营养支持治疗计划。"
                    : "应每周重复进行筛查。"
            }
        };
    }

    private static NutritionAssessmentEvaluation Incomplete(
        IReadOnlyList<string> missingItemCodes) => new()
        {
            IsComplete = false,
            ApplicableItemCodes = ApplicableItemCodes,
            MissingItemCodes = missingItemCodes
        };

    private static NutritionAssessmentOption ScoredOption(
        string code,
        int score,
        string display) => new(code, display, score);

    private static decimal Score(string itemCode, string optionCode) =>
        InstrumentDefinition.Items
            .Single(item => string.Equals(item.Code, itemCode, StringComparison.Ordinal))
            .Options
            .Single(option => string.Equals(option.Code, optionCode, StringComparison.Ordinal))
            .Score
        ?? throw new InvalidOperationException($"NRS 2002 题目 {itemCode} 的选项缺少分值。");

    private static void ValidateAnswers(IReadOnlyDictionary<string, string> answers)
    {
        foreach (var (itemCode, optionCode) in answers)
        {
            var item = InstrumentDefinition.Items.SingleOrDefault(candidate =>
                string.Equals(candidate.Code, itemCode, StringComparison.Ordinal));
            if (item is null || !item.Options.Any(option =>
                    string.Equals(option.Code, optionCode, StringComparison.Ordinal)))
            {
                throw new ArgumentException(
                    $"NRS 2002 回答包含未知题目或选项：{itemCode}。",
                    nameof(answers));
            }
        }
    }
}
