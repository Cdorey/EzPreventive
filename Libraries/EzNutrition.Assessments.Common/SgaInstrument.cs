using EzNutrition.Domain.Assessments;
using EzNutrition.Domain.Consultations;

namespace EzNutrition.Assessments.Common;

/// <summary>
/// 按国家卫生健康委《血液净化标准操作规程（2021版）》实现主观全面评定（SGA）。
/// </summary>
/// <remarks>
/// 评价内容与 A/B/C 判断依据该规程表 27-2、表 27-3。
/// SGA 不采用数值相加；最终等级由专业人员综合病史和体格检查作出。
/// </remarks>
public sealed class SgaInstrument : INutritionAssessmentInstrument
{
    private const string SixMonthWeightCode = "six-month-weight-change";
    private const string TwoWeekWeightCode = "two-week-weight-change";
    private const string IntakeChangeCode = "intake-change";
    private const string IntakeDurationCode = "intake-change-duration";
    private const string GastrointestinalCode = "gastrointestinal-symptoms";
    private const string FunctionCode = "functional-capacity";
    private const string DiseaseStressCode = "disease-and-metabolic-stress";
    private const string SubcutaneousFatCode = "subcutaneous-fat";
    private const string MuscleWastingCode = "muscle-wasting";
    private const string EdemaCode = "edema";
    private const string AscitesCode = "ascites";
    private const string GlobalRatingCode = "global-rating";

    private static readonly string[] ItemCodes =
    [
        SixMonthWeightCode,
        TwoWeekWeightCode,
        IntakeChangeCode,
        IntakeDurationCode,
        GastrointestinalCode,
        FunctionCode,
        DiseaseStressCode,
        SubcutaneousFatCode,
        MuscleWastingCode,
        EdemaCode,
        AscitesCode,
        GlobalRatingCode
    ];

    private static readonly IReadOnlySet<string> ApplicableItemCodes =
        new HashSet<string>(ItemCodes, StringComparer.Ordinal);

    private static readonly NutritionAssessmentDefinition InstrumentDefinition = new()
    {
        CodeSystem = new Uri("https://eznutrition.cdorey.net/codes/nutrition-assessment"),
        Code = "sga",
        Version = "国卫办医函〔2021〕552号",
        DefinitionUri = new Uri(
            "https://www.nhc.gov.cn/wjw/c100175/202111/0f854f7e65cf49bebd930a0f95c8efad/files/1645425894461_83578.pdf"),
        DisplayName = "主观全面评定 SGA",
        Description =
            "本量表依据国家卫生健康委《血液净化标准操作规程（2021版）》表 27-2《主观全面评定（SGA）评价表格》及表 27-3《主观全面评定（SGA）评价标准》。用于血液透析患者营养管理；最终 A/B/C 等级由专业人员综合判断，不采用数值相加。",
        Sections =
        [
            new NutritionAssessmentSection(
                "history",
                "病史评价",
                [
                    ClassifiedItem(
                        SixMonthWeightCode,
                        "6 个月内体重变化",
                        "体重变化 <5%，或下降 5%～10% 但正在改善",
                        "持续下降 5%～10%，或由下降 >10% 改善至 5%～10%",
                        "持续下降 >10%"),
                    ClassifiedItem(
                        TwoWeekWeightCode,
                        "近 2 周体重变化",
                        "无变化、处于正常体重，或恢复至通常体重的 5% 以内",
                        "稳定但低于理想或通常体重，或已有部分恢复但不完全",
                        "继续下降"),
                    ClassifiedItem(
                        IntakeChangeCode,
                        "摄食变化",
                        "良好、无变化，或仅有轻度短期变化",
                        "正常下限但在减少；较差但在增加；或较差且无变化（取决于初始状态）",
                        "较差并继续减少，或较差且无变化"),
                    ClassifiedItem(
                        IntakeDurationCode,
                        "摄食变化持续时间",
                        "≤2 周，变化少或无变化",
                        ">2 周，轻至中度低于理想摄食量",
                        ">2 周，不能进食或处于饥饿状态"),
                    ClassifiedItem(
                        GastrointestinalCode,
                        "近 2 周胃肠道症状（食欲减退、腹泻、恶心、呕吐）",
                        "少有或间断出现",
                        "部分症状持续 >2 周；或严重、持续的症状正在改善",
                        "部分或全部症状频繁或每日出现，并持续 >2 周"),
                    ClassifiedItem(
                        FunctionCode,
                        "功能异常",
                        "无受损；或力气、精力轻至中度下降但正在改善",
                        "力气、精力中度下降但正在改善；通常活动部分减少；或严重下降但正在改善",
                        "力气、精力严重下降或卧床"),
                    ClassifiedItem(
                        DiseaseStressCode,
                        "疾病和相关营养需求（代谢应激）",
                        "无应激",
                        "低水平应激",
                        "中至高度应激")
                ]),
            new NutritionAssessmentSection(
                "physical-examination",
                "体格检查",
                [
                    PhysicalItem(
                        SubcutaneousFatCode,
                        "皮下脂肪（下眼睑、二/三头肌）"),
                    PhysicalItem(
                        MuscleWastingCode,
                        "肌肉消耗（颞部、锁骨、肩、肩胛骨、骨间肌、膝盖、股四头肌、腓肠肌）"),
                    PhysicalItem(EdemaCode, "水肿"),
                    PhysicalItem(AscitesCode, "腹水")
                ],
                "按表 27-3 的观察和触诊要点，将体格检查分别判断为良好、轻中度或重度营养不良。"),
            new NutritionAssessmentSection(
                "global",
                "SGA 综合判断",
                [
                    new NutritionAssessmentItem(
                        GlobalRatingCode,
                        "综合上述病史与体格检查作出 SGA 等级",
                        [
                            new NutritionAssessmentOption("a", "A：营养良好"),
                            new NutritionAssessmentOption("b", "B：轻、中度营养不良"),
                            new NutritionAssessmentOption("c", "C：重度营养不良")
                        ],
                        "综合判断时重点考虑体重下降、膳食摄入减少、皮下脂肪丢失和肌肉消耗，不对各项进行机械加总。")
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
        var missing = NutritionAssessmentInstrumentAnswers.Missing(answers, ItemCodes);
        if (missing.Count > 0)
        {
            return new NutritionAssessmentEvaluation
            {
                IsComplete = false,
                ApplicableItemCodes = ApplicableItemCodes,
                MissingItemCodes = missing
            };
        }

        var rating = NutritionAssessmentInstrumentAnswers.Single(answers, GlobalRatingCode);
        var interpretation = rating switch
        {
            "a" => new NutritionAssessmentInterpretation(
                "well-nourished",
                "A：营养良好",
                NutritionAssessmentAttentionLevel.Routine),
            "b" => new NutritionAssessmentInterpretation(
                "mild-to-moderate-malnutrition",
                "B：轻、中度营养不良",
                NutritionAssessmentAttentionLevel.RequiresAttention),
            "c" => new NutritionAssessmentInterpretation(
                "severe-malnutrition",
                "C：重度营养不良",
                NutritionAssessmentAttentionLevel.RequiresAttention),
            _ => throw new InvalidOperationException("SGA 综合等级无效。")
        };

        return new NutritionAssessmentEvaluation
        {
            IsComplete = true,
            ApplicableItemCodes = ApplicableItemCodes,
            MissingItemCodes = [],
            TotalScore = null,
            Interpretation = interpretation,
            SoapContribution = new SoapContribution
            {
                Objective =
                    "已按《血液净化标准操作规程（2021版）》表 27-2、表 27-3 完成 SGA 病史及体格检查。",
                Assessment = $"主观全面评定（SGA）：{interpretation.Display}。"
            }
        };
    }

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

    private static NutritionAssessmentItem PhysicalItem(string code, string prompt) =>
        ClassifiedItem(
            code,
            prompt,
            "良好",
            "轻至中度营养不良表现",
            "重度营养不良表现");
}
