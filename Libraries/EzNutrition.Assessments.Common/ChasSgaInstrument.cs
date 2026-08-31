using EzNutrition.Domain.Assessments;
using EzNutrition.Domain.Consultations;

namespace EzNutrition.Assessments.Common;

/// <summary>
/// 按 T/CHAS 10-2-29—2020 附录 A6 实现主观整体评估（SGA）。
/// </summary>
/// <remarks>
/// 团体标准以 8 项 A/B/C 分类结果形成整体评价，并规定至少 5 项属于 C 级或
/// B 级时，方可分别判定为重度或轻至中度营养不良。整体等级由专业人员确认，
/// 不将各项机械换算为数值总分。
/// </remarks>
public sealed class ChasSgaInstrument : INutritionAssessmentInstrument
{
    private const string WeightLossCode = "weight-loss";
    private const string DietaryChangeCode = "dietary-change";
    private const string GastrointestinalSymptomsCode = "gastrointestinal-symptoms";
    private const string FunctionalCapacityCode = "functional-capacity";
    private const string StressResponseCode = "stress-response";
    private const string MuscleWastingCode = "muscle-wasting";
    private const string SubcutaneousFatLossCode = "subcutaneous-fat-loss";
    private const string AnkleEdemaCode = "ankle-edema";
    private const string GlobalRatingCode = "global-rating";

    private static readonly string[] AssessmentItemCodes =
    [
        WeightLossCode,
        DietaryChangeCode,
        GastrointestinalSymptomsCode,
        FunctionalCapacityCode,
        StressResponseCode,
        MuscleWastingCode,
        SubcutaneousFatLossCode,
        AnkleEdemaCode
    ];

    private static readonly string[] RequiredItemCodes =
        [.. AssessmentItemCodes, GlobalRatingCode];

    private static readonly IReadOnlySet<string> ApplicableItemCodes =
        new HashSet<string>(RequiredItemCodes, StringComparer.Ordinal);

    private static readonly NutritionAssessmentDefinition InstrumentDefinition = new()
    {
        CodeSystem = new Uri("https://eznutrition.cdorey.net/codes/nutrition-assessment"),
        Code = "sga-chas-2020",
        Version = "T/CHAS 10-2-29—2020",
        DefinitionUri = new Uri("https://www.ttbz.org.cn/StandardManage/Detail/44243"),
        DisplayName = "主观整体评估 SGA（团标版）",
        Description =
            "本量表依据中国医院协会团体标准 T/CHAS 10-2-29—2020《中国医院质量安全管理 第2-29部分：患者服务 临床营养》附录 A6《主观整体评估表（SGA）》。按 8 项 A/B/C 分类结果评价营养状况。",
        Sections =
        [
            new NutritionAssessmentSection(
                "assessment",
                "主观整体评估",
                [
                    ClassifiedItem(
                        WeightLossCode,
                        "体重下降",
                        "近6个月内体重无下降；或近6个月内体重下降＞10%，但近1月内体重又恢复",
                        "近6个月内体重持续性下降达5%～10%",
                        "近6个月体重下降＞10%"),
                    ClassifiedItem(
                        DietaryChangeCode,
                        "饮食改变",
                        "无或较少",
                        "摄食量减少；或呈流质饮食",
                        "摄食严重减少；或呈饥饿状态"),
                    ClassifiedItem(
                        GastrointestinalSymptomsCode,
                        "胃肠道症状（恶心、呕吐、腹泻等）",
                        "无消化道症状",
                        "轻度消化道症状持续时间＜2周",
                        "重度消化道症状持续时间＞2周"),
                    ClassifiedItem(
                        FunctionalCapacityCode,
                        "活动能力",
                        "无限制",
                        "正常活动受限；或虽不能正常活动但卧床或坐椅时间不超过半天",
                        "活动明显受限，仅能卧床或坐椅子；或大部分时间卧床，很少下床活动"),
                    ClassifiedItem(
                        StressResponseCode,
                        "应激反应",
                        "无发热",
                        "近3天体温波动在37℃～39℃之间",
                        "体温≥39℃持续3天以上"),
                    ClassifiedItem(
                        MuscleWastingCode,
                        "肌肉萎缩",
                        "无",
                        "轻度～中度",
                        "重度"),
                    ClassifiedItem(
                        SubcutaneousFatLossCode,
                        "皮下脂肪丢失（肱三头肌皮褶厚度，TSF）",
                        "无",
                        "轻度～中度",
                        "重度"),
                    ClassifiedItem(
                        AnkleEdemaCode,
                        "踝部水肿",
                        "无",
                        "轻度～中度",
                        "重度")
                ]),
            new NutritionAssessmentSection(
                "result",
                "营养状况评估结果",
                [
                    new NutritionAssessmentItem(
                        GlobalRatingCode,
                        "综合上述项目确认营养状况评估结果",
                        [
                            new NutritionAssessmentOption("a", "SGA-A级：营养状况正常"),
                            new NutritionAssessmentOption("b", "SGA-B级：轻～中度营养不良"),
                            new NutritionAssessmentOption("c", "SGA-C级：重度营养不良")
                        ],
                        "上述8项中，至少5项属于C或B级者，才可分别判定为重或中度营养不良。")
                ])
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
        var missing = NutritionAssessmentInstrumentAnswers.Missing(answers, RequiredItemCodes);
        if (missing.Count > 0)
        {
            return new NutritionAssessmentEvaluation
            {
                IsComplete = false,
                ApplicableItemCodes = ApplicableItemCodes,
                MissingItemCodes = missing
            };
        }

        var classACount = CountClassifications(answers, "a");
        var classBCount = CountClassifications(answers, "b");
        var classCCount = CountClassifications(answers, "c");
        var rating = NutritionAssessmentInstrumentAnswers.Single(answers, GlobalRatingCode);
        var interpretation = rating switch
        {
            "a" => new NutritionAssessmentInterpretation(
                "normal-nutritional-status",
                "SGA-A级：营养状况正常",
                NutritionAssessmentAttentionLevel.Routine),
            "b" => new NutritionAssessmentInterpretation(
                "mild-to-moderate-malnutrition",
                "SGA-B级：轻～中度营养不良",
                NutritionAssessmentAttentionLevel.RequiresAttention),
            "c" => new NutritionAssessmentInterpretation(
                "severe-malnutrition",
                "SGA-C级：重度营养不良",
                NutritionAssessmentAttentionLevel.RequiresAttention),
            _ => throw new InvalidOperationException("团标版 SGA 综合等级无效。")
        };

        return new NutritionAssessmentEvaluation
        {
            IsComplete = true,
            ApplicableItemCodes = ApplicableItemCodes,
            MissingItemCodes = [],
            TotalScore = null,
            Metrics =
            [
                new NutritionAssessmentMetric("class-a-item-count", "A级项目数", classACount),
                new NutritionAssessmentMetric("class-b-item-count", "B级项目数", classBCount),
                new NutritionAssessmentMetric("class-c-item-count", "C级项目数", classCCount)
            ],
            Interpretation = interpretation,
            SoapContribution = new SoapContribution
            {
                Objective =
                    $"已按 T/CHAS 10-2-29—2020 附录 A6 完成 SGA 评价：A级 {classACount} 项，B级 {classBCount} 项，C级 {classCCount} 项。",
                Assessment = $"主观整体评估（SGA，团标版）：{interpretation.Display}。"
            }
        };
    }

    private static int CountClassifications(
        IReadOnlyDictionary<string, NutritionAssessmentAnswer> answers,
        string classification) => AssessmentItemCodes.Count(
            itemCode => NutritionAssessmentInstrumentAnswers.Single(answers, itemCode)
                == classification);

    private static NutritionAssessmentItem ClassifiedItem(
        string code,
        string prompt,
        string classA,
        string classB,
        string classC) => new(
            code,
            prompt,
            [
                new NutritionAssessmentOption("a", $"A：{classA}"),
                new NutritionAssessmentOption("b", $"B：{classB}"),
                new NutritionAssessmentOption("c", $"C：{classC}")
            ]);
}
