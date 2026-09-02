using System.Globalization;
using EzNutrition.Domain.Assessments;
using EzNutrition.Domain.Consultations;

namespace EzNutrition.Assessments.Common;

/// <summary>
/// 按 WS/T 888—2026 附录 B 表 B.19 实现医疗机构老年人微营养评定法（简表）（MNA-SF）。
/// </summary>
/// <remarks>
/// 题目、分值与风险阈值依据 WS/T 888—2026《医疗机构老年综合评估技术操作标准》
/// 附录 B 表 B.19。能够从评估对象快照计算 BMI 时直接采用该值，否则显示标准规定的小腿围替代项。
/// </remarks>
public sealed class MnaSfInstrument : INutritionAssessmentInstrument
{
    private const string IntakeCode = "food-intake-decline";
    private const string WeightLossCode = "three-month-weight-loss";
    private const string MobilityCode = "mobility";
    private const string StressCode = "psychological-stress-or-acute-disease";
    private const string NeuropsychologicalCode = "neuropsychological-problems";
    private const string BmiCode = "bmi";
    private const string CalfCircumferenceCode = "calf-circumference";

    private static readonly string[] InterviewItemCodes =
    [
        IntakeCode,
        WeightLossCode,
        MobilityCode,
        StressCode,
        NeuropsychologicalCode
    ];

    private static readonly NutritionAssessmentDefinition InstrumentDefinition = new()
    {
        CodeSystem = new Uri("https://eznutrition.cdorey.net/codes/nutrition-assessment"),
        Code = "mna-sf",
        Version = "WS/T 888—2026",
        DefinitionUri = new Uri(
            "https://www.nhc.gov.cn/wjw/c100310/202603/33c0253d16864df88f36215f186ec13f/files/WST%20888%E2%80%942026-20260318100233155.pdf"),
        DisplayName = "微营养评定法（简表）MNA-SF",
        Description =
            "本量表依据 WS/T 888—2026《医疗机构老年综合评估技术操作标准》附录 B 表 B.19《微营养评定法（简表）》。用于医疗机构老年综合评估中的营养不良风险筛查；总分 ≤11 分提示有营养不良风险。",
        Sections =
        [
            new NutritionAssessmentSection(
                "mna-sf",
                "微营养评定法（简表）",
                [
                    Item(
                        IntakeCode,
                        "过去 3 个月是否因食欲减退、消化不良、咀嚼或吞咽困难而使食量减少？",
                        ("severe", "食量严重减少", 0),
                        ("moderate", "食量中度减少", 1),
                        ("unchanged", "食量没有改变", 2)),
                    Item(
                        WeightLossCode,
                        "过去 3 个月体质量丢失",
                        ("over-three-kilograms", "体质量丢失 >3 kg", 0),
                        ("unknown", "不知道", 1),
                        ("one-to-three-kilograms", "体质量丢失 1 kg～3 kg", 2),
                        ("none", "无", 3)),
                    Item(
                        MobilityCode,
                        "活动能力",
                        ("bed-or-chair-bound", "长期卧床或坐轮椅", 0),
                        ("indoors-only", "可以下床或离开轮椅，但不能外出", 1),
                        ("goes-out", "可以外出", 2)),
                    Item(
                        StressCode,
                        "过去 3 个月是否受到心理创伤或有急性疾病？",
                        ("yes", "是", 0),
                        ("no", "否", 2)),
                    Item(
                        NeuropsychologicalCode,
                        "精神心理问题",
                        ("severe", "严重痴呆或抑郁", 0),
                        ("mild-dementia", "轻度痴呆", 1),
                        ("none", "无精神心理问题", 2)),
                    Item(
                        BmiCode,
                        "体质指数（BMI）",
                        ("below-19", "BMI <19 kg/m²", 0),
                        ("19-to-below-21", "19 kg/m²≤ BMI <21 kg/m²", 1),
                        ("21-to-below-23", "21 kg/m²≤ BMI <23 kg/m²", 2),
                        ("at-least-23", "BMI ≥23 kg/m²", 3)),
                    Item(
                        CalfCircumferenceCode,
                        "无法取得 BMI 时的小腿围（CC）",
                        ("below-31", "CC <31 cm", 0),
                        ("at-least-31", "CC ≥31 cm", 3))
                ],
                "优先采用 BMI；无法取得 BMI 时以小腿围代替。小腿围按健侧小腿最大围长测量。")
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

        var usesBmi = subject.BodyMassIndex is not null;
        var applicable = new HashSet<string>(InterviewItemCodes, StringComparer.Ordinal);
        if (!usesBmi)
        {
            applicable.Add(CalfCircumferenceCode);
        }

        var required = usesBmi
            ? InterviewItemCodes
            : [.. InterviewItemCodes, CalfCircumferenceCode];
        var missing = NutritionAssessmentInstrumentAnswers.Missing(answers, required);
        if (missing.Count > 0)
        {
            return Incomplete(applicable, missing);
        }

        var intakeScore = Score(answers, IntakeCode);
        var weightScore = Score(answers, WeightLossCode);
        var mobilityScore = Score(answers, MobilityCode);
        var stressScore = Score(answers, StressCode);
        var neuropsychologicalScore = Score(answers, NeuropsychologicalCode);
        var anthropometricScore = usesBmi
            ? BmiScore(subject.BodyMassIndex!.Value)
            : Score(answers, CalfCircumferenceCode);
        var total = intakeScore + weightScore + mobilityScore + stressScore
            + neuropsychologicalScore + anthropometricScore;
        var atRisk = total <= 11m;
        var interpretation = atRisk
            ? new NutritionAssessmentInterpretation(
                "malnutrition-risk",
                "有营养不良风险",
                NutritionAssessmentAttentionLevel.RequiresAttention)
            : new NutritionAssessmentInterpretation(
                "no-malnutrition-risk",
                "未提示营养不良风险",
                NutritionAssessmentAttentionLevel.Routine);
        var totalText = total.ToString(CultureInfo.InvariantCulture);

        return new NutritionAssessmentEvaluation
        {
            IsComplete = true,
            ApplicableItemCodes = applicable,
            MissingItemCodes = [],
            TotalScore = total,
            Metrics =
            [
                new NutritionAssessmentMetric("intake-score", "食量评分", intakeScore),
                new NutritionAssessmentMetric("weight-loss-score", "体重下降评分", weightScore),
                new NutritionAssessmentMetric("mobility-score", "活动能力评分", mobilityScore),
                new NutritionAssessmentMetric("stress-score", "心理创伤或急性疾病评分", stressScore),
                new NutritionAssessmentMetric(
                    "neuropsychological-score",
                    "神经精神问题评分",
                    neuropsychologicalScore),
                new NutritionAssessmentMetric(
                    usesBmi ? "bmi-score" : "calf-circumference-score",
                    usesBmi ? "BMI 评分" : "小腿围评分",
                    anthropometricScore)
            ],
            Interpretation = interpretation,
            SoapContribution = new SoapContribution
            {
                Objective =
                    $"微营养评定法（简表）（MNA-SF，WS/T 888—2026 附录 B 表 B.19）总分 {totalText} 分。",
                Assessment = $"MNA-SF 结果：{interpretation.Display}。",
                Plan = atRisk ? "建议进一步进行营养评估并结合临床情况处理。" : null
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

    private static decimal Score(
        IReadOnlyDictionary<string, NutritionAssessmentAnswer> answers,
        string itemCode) => NutritionAssessmentInstrumentAnswers.Score(
            InstrumentDefinition,
            answers,
            itemCode);

    private static decimal BmiScore(decimal bodyMassIndex) => bodyMassIndex switch
    {
        < 19m => 0m,
        < 21m => 1m,
        < 23m => 2m,
        _ => 3m
    };

    private static NutritionAssessmentItem Item(
        string code,
        string prompt,
        params (string Code, string Display, int Score)[] options) => new(
            code,
            prompt,
            options.Select(option => new NutritionAssessmentOption(
                option.Code,
                option.Display,
                option.Score)).ToArray());
}
