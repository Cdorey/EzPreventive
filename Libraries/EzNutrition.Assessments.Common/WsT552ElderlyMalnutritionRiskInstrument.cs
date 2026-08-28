using System.Globalization;
using EzNutrition.Domain.Assessments;
using EzNutrition.Domain.Consultations;

namespace EzNutrition.Assessments.Common;

/// <summary>
/// 按 WS/T 552—2017 实现 65 岁及以上老年人营养不良风险评估。
/// </summary>
/// <remarks>
/// 初筛、后续评估、年龄调整与结果判定依据 WS/T 552—2017
/// 第 3.5 条及规范性附录 A 表 A.1。
/// </remarks>
public sealed class WsT552ElderlyMalnutritionRiskInstrument :
    INutritionAssessmentInstrument
{
    private const string BmiCode = "bmi";
    private const string WeightChangeCode = "three-month-weight-change";
    private const string MobilityCode = "mobility";
    private const string DentalCode = "dental-status";
    private const string NeuropsychologicalCode = "neuropsychological-disorder";
    private const string IntakeChangeCode = "three-month-intake-change";
    private const string ChronicDiseasesCode = "more-than-three-chronic-diseases";
    private const string MedicationsCode = "more-than-three-long-term-medications";
    private const string LivingAloneCode = "living-alone";
    private const string SleepCode = "sleep-duration";
    private const string OutdoorActivityCode = "independent-outdoor-activity";
    private const string EducationCode = "education";
    private const string EconomyCode = "perceived-economic-status";
    private const string FeedingCode = "feeding-ability";
    private const string MealsCode = "daily-meals";
    private const string ProteinFoodsCode = "daily-protein-food-groups";
    private const string CookingOilCode = "daily-cooking-oil";
    private const string FruitVegetablesCode = "daily-fruit-and-vegetables";
    private const string CalfCircumferenceCode = "calf-circumference";
    private const string WaistCircumferenceCode = "waist-circumference";

    private static readonly string[] ScreeningCodes =
    [
        BmiCode,
        WeightChangeCode,
        MobilityCode,
        DentalCode,
        NeuropsychologicalCode,
        IntakeChangeCode
    ];

    private static readonly string[] AssessmentCodes =
    [
        ChronicDiseasesCode,
        MedicationsCode,
        LivingAloneCode,
        SleepCode,
        OutdoorActivityCode,
        EducationCode,
        EconomyCode,
        FeedingCode,
        MealsCode,
        ProteinFoodsCode,
        CookingOilCode,
        FruitVegetablesCode,
        CalfCircumferenceCode,
        WaistCircumferenceCode
    ];

    private static readonly IReadOnlySet<string> ScreeningItemCodes =
        new HashSet<string>(ScreeningCodes, StringComparer.Ordinal);

    private static readonly NutritionAssessmentDefinition InstrumentDefinition = new()
    {
        CodeSystem = new Uri("https://eznutrition.cdorey.net/codes/nutrition-assessment"),
        Code = "ws-t-552-elderly-malnutrition-risk",
        Version = "WS/T 552—2017",
        DefinitionUri = new Uri(
            "https://www.nhc.gov.cn/ewebeditor/uploadfile/2017/08/20170811093418434.pdf"),
        DisplayName = "老年人营养不良风险评估（WS/T 552）",
        Description =
            "本量表依据 WS/T 552—2017《老年人营养不良风险评估》第 3.5 条及规范性附录 A 表 A.1。适用于 65 岁及以上老年人；初筛 <12 分时继续完成后续评估。",
        Sections =
        [
            new NutritionAssessmentSection(
                "screening",
                "初筛（0 分～14 分）",
                [
                    Item(
                        BmiCode,
                        "1. 体质指数（BMI）",
                        ("below-19", "BMI <19 kg/m²", 0m),
                        ("19-to-below-21", "19 kg/m²≤ BMI <21 kg/m²", 1m),
                        ("21-to-below-23", "21 kg/m²≤ BMI <23 kg/m²", 2m),
                        ("23-to-below-24", "23 kg/m²≤ BMI <24 kg/m²", 3m),
                        ("exactly-24", "BMI =24 kg/m²", 3m),
                        ("above-24-to-26", "24 kg/m²< BMI ≤26 kg/m²", 2m),
                        ("above-26-to-28", "26 kg/m²< BMI ≤28 kg/m²", 1m),
                        ("above-28", "BMI >28 kg/m²", 0m)),
                    Item(
                        WeightChangeCode,
                        "2. 近 3 个月体重变化",
                        ("over-three-kilograms", "减少或增加 >3 kg", 0m),
                        ("unknown", "不知道", 1m),
                        ("one-to-three-kilograms", "减少或增加 1 kg～3 kg", 2m),
                        ("below-one-kilogram", "体重无变化，或减少/增加 >0 kg 且 <1 kg", 3m)),
                    Item(
                        MobilityCode,
                        "3. 活动能力",
                        ("bedridden", "卧床", 0m),
                        ("assistive-device", "需要依赖工具活动", 1m),
                        ("independent-outdoors", "独立户外活动", 2m)),
                    Item(
                        DentalCode,
                        "4. 牙齿状况",
                        ("full-or-half-missing", "全口或半口缺齿", 0m),
                        ("dentures", "使用义齿", 1m),
                        ("normal", "正常", 2m)),
                    Item(
                        NeuropsychologicalCode,
                        "5. 神经精神疾病",
                        ("severe", "严重认知障碍或抑郁", 0m),
                        ("mild", "轻度认知障碍或抑郁", 1m),
                        ("none", "无认知障碍或抑郁", 2m)),
                    Item(
                        IntakeChangeCode,
                        "6. 近 3 个月有无饮食量变化",
                        ("severe", "严重增加或减少", 0m),
                        ("changed", "增加或减少", 1m),
                        ("unchanged", "无变化", 2m))
                ],
                "初筛总分 <12 分提示有营养不良风险，继续后续评估；≥12 分提示无营养不良风险，无需后续评估。"),
            new NutritionAssessmentSection(
                "assessment",
                "后续评估",
                [
                    YesNoItem(ChronicDiseasesCode, "7. 患慢性病数 >3 种", yesScore: 0m, noScore: 1m),
                    YesNoItem(MedicationsCode, "8. 服用时间 >1 个月的药物种类 >3 种", yesScore: 0m, noScore: 1m),
                    YesNoItem(LivingAloneCode, "9. 是否独居", yesScore: 0m, noScore: 1m),
                    Item(
                        SleepCode,
                        "10. 睡眠时间",
                        ("below-five-hours", "<5 h/d", 0m),
                        ("at-least-five-hours", "≥5 h/d", 1m)),
                    Item(
                        OutdoorActivityCode,
                        "11. 户外独立活动时间",
                        ("below-one-hour", "<1 h/d", 0m),
                        ("at-least-one-hour", "≥1 h/d", 1m)),
                    Item(
                        EducationCode,
                        "12. 文化程度",
                        ("primary-or-below", "小学及以下", 0m),
                        ("middle-or-above", "中学及以上", 1m)),
                    Item(
                        EconomyCode,
                        "13. 自我感觉经济状况",
                        ("poor", "差", 0m),
                        ("fair", "一般", 0.5m),
                        ("good", "良好", 1m)),
                    Item(
                        FeedingCode,
                        "14. 进食能力",
                        ("dependent", "依靠别人", 0m),
                        ("some-difficulty", "自行进食稍有困难", 1m),
                        ("independent", "自行进食", 2m)),
                    Item(
                        MealsCode,
                        "15. 一天餐次",
                        ("one", "1 次", 0m),
                        ("two", "2 次", 1m),
                        ("three-or-more", "3 次及以上", 2m)),
                    Item(
                        ProteinFoodsCode,
                        "16. 每天摄入奶类、豆制品、鱼/肉/禽/蛋类食品的项数",
                        ("zero-or-one", "0～1 项", 0m),
                        ("two", "2 项", 0.5m),
                        ("three", "3 项", 1m)),
                    Item(
                        CookingOilCode,
                        "17. 每天烹调油摄入量",
                        ("above-25-grams", ">25 g", 0m),
                        ("at-most-25-grams", "≤25 g", 1m)),
                    YesNoItem(
                        FruitVegetablesCode,
                        "18. 是否每天吃蔬菜水果 500 g 及以上",
                        yesScore: 1m,
                        noScore: 0m),
                    Item(
                        CalfCircumferenceCode,
                        "19. 小腿围",
                        ("below-31", "<31 cm", 0m),
                        ("at-least-31", "≥31 cm", 1m)),
                    Item(
                        WaistCircumferenceCode,
                        "20. 腰围",
                        ("above-threshold", "男性 >90 cm；女性 >80 cm", 0m),
                        ("within-threshold", "男性 ≤90 cm；女性 ≤80 cm", 1m))
                ],
                "初筛与后续评估得分相加；年龄 ≥70 岁时总分另加 1 分。")
        ]
    };

    /// <inheritdoc />
    public NutritionAssessmentDefinition Definition => InstrumentDefinition;

    /// <inheritdoc />
    public NutritionAssessmentEvaluation Evaluate(
        IReadOnlyDictionary<string, NutritionAssessmentAnswer> answers,
        NutritionAssessmentSubject subject)
    {
        ArgumentNullException.ThrowIfNull(answers);
        ArgumentNullException.ThrowIfNull(subject);
        NutritionAssessmentInstrumentAnswers.Validate(InstrumentDefinition, answers);
        var screeningMissing = NutritionAssessmentInstrumentAnswers.Missing(
            answers,
            ScreeningCodes);
        if (screeningMissing.Count > 0)
        {
            return Incomplete(ScreeningItemCodes, screeningMissing);
        }

        var screeningScore = ScreeningCodes.Sum(code => Score(answers, code));
        if (screeningScore >= 12m)
        {
            var screeningInterpretation = new NutritionAssessmentInterpretation(
                "no-malnutrition-risk",
                "无营养不良风险",
                NutritionAssessmentAttentionLevel.Routine);
            return new NutritionAssessmentEvaluation
            {
                IsComplete = true,
                ApplicableItemCodes = ScreeningItemCodes,
                MissingItemCodes = [],
                TotalScore = screeningScore,
                Metrics =
                [
                    new NutritionAssessmentMetric("screening-score", "初筛总分", screeningScore)
                ],
                Interpretation = screeningInterpretation,
                SoapContribution = new SoapContribution
                {
                    Objective =
                        $"老年人营养不良风险评估（WS/T 552—2017）初筛 {screeningScore:0.#} 分。",
                    Assessment = $"初筛结果：{screeningInterpretation.Display}。"
                }
            };
        }

        var applicable = new HashSet<string>(ScreeningCodes, StringComparer.Ordinal);
        applicable.UnionWith(AssessmentCodes);
        var assessmentMissing = NutritionAssessmentInstrumentAnswers.Missing(
            answers,
            AssessmentCodes);
        if (assessmentMissing.Count > 0)
        {
            return Incomplete(applicable, assessmentMissing);
        }

        var assessmentScore = AssessmentCodes.Sum(code => Score(answers, code));
        var ageScore = subject.AgeInYears >= 70 ? 1m : 0m;
        var total = screeningScore + assessmentScore + ageScore;
        var overweightOrObese = IsOverweightOrObese(answers);
        var interpretation = FullInterpretation(total, overweightOrObese);
        var totalText = total.ToString("0.#", CultureInfo.InvariantCulture);

        return new NutritionAssessmentEvaluation
        {
            IsComplete = true,
            ApplicableItemCodes = applicable,
            MissingItemCodes = [],
            TotalScore = total,
            Metrics =
            [
                new NutritionAssessmentMetric("screening-score", "初筛总分", screeningScore),
                new NutritionAssessmentMetric("assessment-score", "后续评估总分", assessmentScore),
                new NutritionAssessmentMetric("age-score", "年龄调整分", ageScore)
            ],
            Interpretation = interpretation,
            SoapContribution = new SoapContribution
            {
                Objective =
                    $"老年人营养不良风险评估（WS/T 552—2017）：初筛 {screeningScore:0.#} 分，后续评估 {assessmentScore:0.#} 分，年龄调整 {ageScore:0} 分，总分 {totalText} 分。",
                Assessment = $"评估结果：{interpretation.Display}。",
                Plan = interpretation.AttentionLevel == NutritionAssessmentAttentionLevel.RequiresAttention
                    ? "建议结合临床资料进一步评估营养状况并制定相应处理计划。"
                    : null
            }
        };
    }

    private static NutritionAssessmentEvaluation Incomplete(
        IReadOnlySet<string> applicable,
        IReadOnlyList<string> missing) => new()
        {
            IsComplete = false,
            ApplicableItemCodes = applicable,
            MissingItemCodes = missing
        };

    private static bool IsOverweightOrObese(
        IReadOnlyDictionary<string, NutritionAssessmentAnswer> answers)
    {
        var bmi = NutritionAssessmentInstrumentAnswers.Single(answers, BmiCode);
        var waist = NutritionAssessmentInstrumentAnswers.Single(
            answers,
            WaistCircumferenceCode);
        return bmi is "exactly-24" or "above-24-to-26" or "above-26-to-28" or "above-28"
            || waist == "above-threshold";
    }

    private static NutritionAssessmentInterpretation FullInterpretation(
        decimal total,
        bool overweightOrObese)
    {
        if (total >= 24m)
        {
            return new NutritionAssessmentInterpretation(
                "good-nutritional-status",
                "营养状况良好",
                NutritionAssessmentAttentionLevel.Routine);
        }

        if (overweightOrObese)
        {
            return new NutritionAssessmentInterpretation(
                "possible-overweight-obese-malnutrition-or-risk",
                "可能为超重/肥胖型营养不良或有营养不良风险",
                NutritionAssessmentAttentionLevel.RequiresAttention);
        }

        return total > 17m
            ? new NutritionAssessmentInterpretation(
                "malnutrition-risk",
                "有营养不良风险",
                NutritionAssessmentAttentionLevel.RequiresAttention)
            : new NutritionAssessmentInterpretation(
                "malnutrition",
                "有营养不良",
                NutritionAssessmentAttentionLevel.RequiresAttention);
    }

    private static decimal Score(
        IReadOnlyDictionary<string, NutritionAssessmentAnswer> answers,
        string itemCode) => NutritionAssessmentInstrumentAnswers.Score(
            InstrumentDefinition,
            answers,
            itemCode);

    private static NutritionAssessmentItem YesNoItem(
        string code,
        string prompt,
        decimal yesScore,
        decimal noScore) => Item(
            code,
            prompt,
            ("yes", "是", yesScore),
            ("no", "否", noScore));

    private static NutritionAssessmentItem Item(
        string code,
        string prompt,
        params (string Code, string Display, decimal Score)[] options) => new(
            code,
            prompt,
            options.Select(option => new NutritionAssessmentOption(
                option.Code,
                option.Display,
                option.Score)).ToArray());
}
