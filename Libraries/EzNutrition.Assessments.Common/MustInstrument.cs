using System.Globalization;
using EzNutrition.Domain.Assessments;
using EzNutrition.Domain.Consultations;

namespace EzNutrition.Assessments.Common;

/// <summary>
/// 按 BAPEN 五步流程实现成年人营养不良通用筛查工具（MUST）的筛查与风险分级。
/// </summary>
/// <remarks>
/// 题目、分值和低/中/高风险阈值依据 BAPEN 发布的
/// <c>Malnutrition Universal Screening Tool</c> Steps 1–4。
/// </remarks>
public sealed class MustInstrument : INutritionAssessmentInstrument
{
    private const string BmiCode = "bmi-score";
    private const string WeightLossCode = "unplanned-weight-loss";
    private const string AcuteDiseaseCode = "acute-disease-effect";

    private static readonly string[] ItemCodes = [BmiCode, WeightLossCode, AcuteDiseaseCode];
    private static readonly IReadOnlySet<string> ApplicableItemCodes =
        new HashSet<string>(ItemCodes, StringComparer.Ordinal);

    private static readonly NutritionAssessmentDefinition InstrumentDefinition = new()
    {
        CodeSystem = new Uri("https://eznutrition.cdorey.net/codes/nutrition-assessment"),
        Code = "must",
        Version = "BAPEN MUST",
        DefinitionUri = new Uri("https://www.bapen.org.uk/pdfs/must/must_full.pdf"),
        DisplayName = "营养不良通用筛查工具 MUST",
        Description =
            "本量表依据 BAPEN《Malnutrition Universal Screening Tool》五步流程中的 Steps 1–4。用于成年人在医院、社区及其他照护场景识别营养不良风险。",
        Sections =
        [
            new NutritionAssessmentSection(
                "must-steps-1-to-3",
                "MUST 评分",
                [
                    Item(
                        BmiCode,
                        "Step 1：BMI 评分",
                        ("above-20", "BMI >20 kg/m²（BMI >30 kg/m² 时同时记录肥胖）", 0),
                        ("18-5-to-20", "BMI 18.5 kg/m²～20 kg/m²", 1),
                        ("below-18-5", "BMI <18.5 kg/m²", 2)),
                    Item(
                        WeightLossCode,
                        "Step 2：过去 3～6 个月非计划性体重下降",
                        ("below-five-percent", "<5%", 0),
                        ("five-to-ten-percent", "5%～10%", 1),
                        ("above-ten-percent", ">10%", 2)),
                    Item(
                        AcuteDiseaseCode,
                        "Step 3：急性疾病影响",
                        ("absent", "不符合急性疾病影响条件", 0),
                        ("present", "患者病情急性，且已经或预计超过 5 天无营养摄入", 2))
                ],
                "总分为 BMI、非计划性体重下降和急性疾病影响三项之和。无法取得身高和体重时，应按 MUST 原流程采用替代测量和主观标准。")
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

        var bmiScore = Score(answers, BmiCode);
        var weightLossScore = Score(answers, WeightLossCode);
        var acuteDiseaseScore = Score(answers, AcuteDiseaseCode);
        var total = bmiScore + weightLossScore + acuteDiseaseScore;
        var interpretation = total switch
        {
            0m => new NutritionAssessmentInterpretation(
                "low-risk",
                "营养不良低风险",
                NutritionAssessmentAttentionLevel.Routine),
            1m => new NutritionAssessmentInterpretation(
                "medium-risk",
                "营养不良中风险",
                NutritionAssessmentAttentionLevel.RequiresAttention),
            _ => new NutritionAssessmentInterpretation(
                "high-risk",
                "营养不良高风险",
                NutritionAssessmentAttentionLevel.RequiresAttention)
        };
        var totalText = total.ToString(CultureInfo.InvariantCulture);

        return new NutritionAssessmentEvaluation
        {
            IsComplete = true,
            ApplicableItemCodes = ApplicableItemCodes,
            MissingItemCodes = [],
            TotalScore = total,
            Metrics =
            [
                new NutritionAssessmentMetric("bmi-score", "BMI 评分", bmiScore),
                new NutritionAssessmentMetric(
                    "weight-loss-score",
                    "非计划性体重下降评分",
                    weightLossScore),
                new NutritionAssessmentMetric(
                    "acute-disease-score",
                    "急性疾病影响评分",
                    acuteDiseaseScore)
            ],
            Interpretation = interpretation,
            SoapContribution = new SoapContribution
            {
                Objective =
                    $"营养不良通用筛查工具（MUST）：BMI {bmiScore:0} 分，非计划性体重下降 {weightLossScore:0} 分，急性疾病影响 {acuteDiseaseScore:0} 分，总分 {totalText} 分。",
                Assessment = $"MUST 结果：{interpretation.Display}。",
                Plan = total switch
                {
                    0m => "按所在照护场景的 MUST 流程定期复筛。",
                    1m => "记录膳食摄入并结合所在照护场景重复筛查；必要时按临床情况处理。",
                    _ => "转介营养专业人员或营养支持团队，制定并监测营养照护计划。"
                }
            }
        };
    }

    private static decimal Score(
        IReadOnlyDictionary<string, NutritionAssessmentAnswer> answers,
        string itemCode) => NutritionAssessmentInstrumentAnswers.Score(
            InstrumentDefinition,
            answers,
            itemCode);

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
