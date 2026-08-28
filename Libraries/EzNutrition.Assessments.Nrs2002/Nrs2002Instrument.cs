using System.Globalization;
using EzNutrition.Domain.Assessments;
using EzNutrition.Domain.Consultations;

namespace EzNutrition.Assessments.Nrs2002;

/// <summary>
/// 实现住院患者营养风险筛查 NRS 2002 的题目、分支、计分和结果解释。
/// </summary>
/// <remarks>
/// 规则依据 Kondrup 等人的 NRS 2002 原始方法（DOI: 10.1016/S0261-5614(02)00214-5）
/// 及 ESPEN 营养筛查指南（DOI: 10.1016/S0261-5614(03)00098-0）。中文显示文本为便于本项目
/// 使用而作的释义；临床使用者仍应结合正式量表、机构流程和专业判断。
/// </remarks>
public sealed class Nrs2002Instrument : INutritionAssessmentInstrument
{
    private const string InitialBmiCode = "initial-bmi-below-20-5";
    private const string InitialWeightLossCode = "initial-weight-loss-within-three-months";
    private const string InitialReducedIntakeCode = "initial-reduced-intake-last-week";
    private const string InitialSevereIllnessCode = "initial-severe-illness";
    private const string NutritionalStatusCode = "impaired-nutritional-status";
    private const string DiseaseSeverityCode = "disease-severity";
    private const string YesCode = "yes";
    private const string NoCode = "no";

    private static readonly string[] InitialItemCodes =
    [
        InitialBmiCode,
        InitialWeightLossCode,
        InitialReducedIntakeCode,
        InitialSevereIllnessCode
    ];

    private static readonly IReadOnlySet<string> InitialApplicableItemCodes =
        new HashSet<string>(InitialItemCodes, StringComparer.Ordinal);

    private static readonly IReadOnlySet<string> FinalApplicableItemCodes =
        new HashSet<string>(
            [.. InitialItemCodes, NutritionalStatusCode, DiseaseSeverityCode],
            StringComparer.Ordinal);

    private static readonly NutritionAssessmentDefinition InstrumentDefinition = new()
    {
        CodeSystem = new Uri("https://eznutrition.cdorey.net/codes/nutrition-assessment"),
        Code = "nrs-2002",
        Version = "2002",
        DefinitionUri = new Uri("https://doi.org/10.1016/S0261-5614(03)00098-0"),
        DisplayName = "营养风险筛查 NRS 2002",
        Description = "适用范围：成年住院患者。用于营养风险初筛与终筛；结果应结合机构流程和专业判断。",
        Sections =
        [
            new NutritionAssessmentSection(
                "initial-screening",
                "初筛",
                [
                    YesNoItem(
                        InitialBmiCode,
                        "BMI 是否低于 20.5 kg/m²？",
                        "可参考本次咨询已记录的身高、体重及其计算结果。"),
                    YesNoItem(
                        InitialWeightLossCode,
                        "最近 3 个月内是否出现体重下降？"),
                    YesNoItem(
                        InitialReducedIntakeCode,
                        "最近 1 周的膳食摄入是否减少？"),
                    YesNoItem(
                        InitialSevereIllnessCode,
                        "患者目前是否病情严重（例如接受重症监护）？")
                ],
                "四项均为“否”时不进入终筛；任一项为“是”时继续完成终筛。"),
            new NutritionAssessmentSection(
                "final-screening",
                "终筛",
                [
                    new NutritionAssessmentItem(
                        NutritionalStatusCode,
                        "营养状态受损程度",
                        [
                            ScoredOption(0, "无：营养状态正常"),
                            ScoredOption(1, "轻度：3 个月内体重下降超过 5%，或上周摄入约为正常需要量的 50%～75%"),
                            ScoredOption(2, "中度：2 个月内体重下降超过 5%；或 BMI 18.5～20.5 kg/m² 且一般情况受损；或上周摄入约为正常需要量的 25%～60%"),
                            ScoredOption(3, "重度：1 个月内体重下降超过 5%（或 3 个月内超过 15%）；或 BMI 低于 18.5 kg/m² 且一般情况受损；或上周摄入约为正常需要量的 0%～25%")
                        ]),
                    new NutritionAssessmentItem(
                        DiseaseSeverityCode,
                        "疾病严重程度（营养需要量增加程度）",
                        [
                            ScoredOption(0, "无：营养需要量正常"),
                            ScoredOption(1, "轻度：例如髋部骨折，或伴急性并发症的慢性疾病、慢性血液透析、糖尿病、肿瘤等"),
                            ScoredOption(2, "中度：例如腹部大手术、卒中、重症肺炎、血液系统恶性肿瘤等"),
                            ScoredOption(3, "重度：例如颅脑损伤、骨髓移植，或 APACHE II 评分高于 10 分的重症监护患者等")
                        ],
                        "示例用于辅助分级，不替代对患者实际代谢应激和营养需要量的专业判断。")
                ],
                "终筛总分为营养状态受损评分与疾病严重程度评分之和；年龄达到 70 岁时再加 1 分。")
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

        var missingInitialItems = InitialItemCodes
            .Where(code => !answers.ContainsKey(code))
            .ToArray();
        if (missingInitialItems.Length > 0)
        {
            return Incomplete(InitialApplicableItemCodes, missingInitialItems);
        }

        var requiresFinalScreening = InitialItemCodes.Any(code =>
            string.Equals(answers[code], YesCode, StringComparison.Ordinal));
        if (!requiresFinalScreening)
        {
            return CreateNegativeInitialScreeningResult();
        }

        var missingFinalItems = new[] { NutritionalStatusCode, DiseaseSeverityCode }
            .Where(code => !answers.ContainsKey(code))
            .ToArray();
        if (missingFinalItems.Length > 0)
        {
            return Incomplete(FinalApplicableItemCodes, missingFinalItems);
        }

        var nutritionalStatusScore = ParseScore(answers[NutritionalStatusCode]);
        var diseaseSeverityScore = ParseScore(answers[DiseaseSeverityCode]);
        var ageAdjustment = subject.AgeInYears >= 70 ? 1m : 0m;
        var totalScore = nutritionalStatusScore + diseaseSeverityScore + ageAdjustment;
        var atRisk = totalScore >= 3m;
        var totalText = totalScore.ToString(CultureInfo.InvariantCulture);
        var interpretation = atRisk
            ? new NutritionAssessmentInterpretation("nutritional-risk", "存在营养风险")
            : new NutritionAssessmentInterpretation("below-risk-threshold", "未达到营养风险判定阈值");

        return new NutritionAssessmentEvaluation
        {
            IsComplete = true,
            ApplicableItemCodes = FinalApplicableItemCodes,
            MissingItemCodes = [],
            TotalScore = totalScore,
            Metrics =
            [
                new NutritionAssessmentMetric(
                    "impaired-nutritional-status-score",
                    "营养状态受损评分",
                    nutritionalStatusScore),
                new NutritionAssessmentMetric(
                    "disease-severity-score",
                    "疾病严重程度评分",
                    diseaseSeverityScore),
                new NutritionAssessmentMetric(
                    "age-adjustment-score",
                    "年龄校正分",
                    ageAdjustment)
            ],
            Interpretation = interpretation,
            SoapContribution = new SoapContribution
            {
                Objective = $"NRS 2002：营养状态受损 {nutritionalStatusScore:0} 分，疾病严重程度 {diseaseSeverityScore:0} 分，年龄校正 {ageAdjustment:0} 分，总分 {totalText} 分。",
                Assessment = $"NRS 2002 筛查结果：{interpretation.Display}（总分 {totalText} 分）。",
                Plan = atRisk
                    ? "根据 NRS 2002 筛查结果，建议进一步开展营养评估并制定营养照护计划。"
                    : "住院期间建议每周复筛；如拟行重大手术，可结合专业判断考虑预防性营养照护计划。"
            }
        };
    }

    private static NutritionAssessmentEvaluation CreateNegativeInitialScreeningResult() => new()
    {
        IsComplete = true,
        ApplicableItemCodes = InitialApplicableItemCodes,
        MissingItemCodes = [],
        Interpretation = new NutritionAssessmentInterpretation(
            "negative-initial-screening",
            "初筛阴性，未进入终筛"),
        SoapContribution = new SoapContribution
        {
            Objective = "NRS 2002 初筛四项均为否，未进入终筛。",
            Assessment = "NRS 2002 筛查结果：初筛阴性，当前无需进入终筛。",
            Plan = "住院期间建议每周复筛；如拟行重大手术，可结合专业判断考虑预防性营养照护计划。"
        }
    };

    private static NutritionAssessmentEvaluation Incomplete(
        IReadOnlySet<string> applicableItemCodes,
        IReadOnlyList<string> missingItemCodes) => new()
        {
            IsComplete = false,
            ApplicableItemCodes = applicableItemCodes,
            MissingItemCodes = missingItemCodes
        };

    private static NutritionAssessmentItem YesNoItem(
        string code,
        string prompt,
        string? helpText = null) => new(
            code,
            prompt,
            [
                new NutritionAssessmentOption(YesCode, "是"),
                new NutritionAssessmentOption(NoCode, "否")
            ],
            helpText);

    private static NutritionAssessmentOption ScoredOption(int score, string display) =>
        new(score.ToString(CultureInfo.InvariantCulture), display, score);

    private static decimal ParseScore(string value)
    {
        if (!decimal.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var score)
            || score is < 0m or > 3m)
        {
            throw new ArgumentException("NRS 2002 终筛回答不是有效的 0～3 分选项。", nameof(value));
        }

        return score;
    }

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
