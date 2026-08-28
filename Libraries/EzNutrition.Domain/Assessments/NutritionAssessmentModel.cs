using EzNutrition.Domain.Consultations;

namespace EzNutrition.Domain.Assessments;

/// <summary>
/// 表示一个量表选项及其可选计分贡献。
/// </summary>
/// <param name="Code">选项在当前量表版本内的稳定编码。</param>
/// <param name="Display">向专业人员展示的选项文本。</param>
/// <param name="Score">该选项直接贡献的分值；不直接参与总分时为空。</param>
public sealed record NutritionAssessmentOption(
    string Code,
    string Display,
    decimal? Score = null);

/// <summary>
/// 表示一个由单选选项回答的量表题目。
/// </summary>
/// <param name="Code">题目在当前量表版本内的稳定编码。</param>
/// <param name="Prompt">向专业人员展示的题目文本。</param>
/// <param name="Options">该题允许选择的选项。</param>
/// <param name="HelpText">不参与计分的可选说明。</param>
public sealed record NutritionAssessmentItem(
    string Code,
    string Prompt,
    IReadOnlyList<NutritionAssessmentOption> Options,
    string? HelpText = null);

/// <summary>
/// 表示量表中具有共同临床含义的一组题目。
/// </summary>
/// <param name="Code">分组在当前量表版本内的稳定编码。</param>
/// <param name="Title">分组标题。</param>
/// <param name="Items">分组内按正式题序排列的题目。</param>
/// <param name="Description">可选分组说明。</param>
public sealed record NutritionAssessmentSection(
    string Code,
    string Title,
    IReadOnlyList<NutritionAssessmentItem> Items,
    string? Description = null);

/// <summary>
/// 表示一个具有稳定身份和确切版本的营养量表定义。
/// </summary>
public sealed record NutritionAssessmentDefinition
{
    /// <summary>获取控制量表、题目、选项和结果编码的代码体系。</summary>
    public required Uri CodeSystem { get; init; }

    /// <summary>获取量表在代码体系中的稳定编码。</summary>
    public required string Code { get; init; }

    /// <summary>获取量表临床定义的确切版本。</summary>
    public required string Version { get; init; }

    /// <summary>获取量表正式定义或主要依据的规范地址。</summary>
    public required Uri DefinitionUri { get; init; }

    /// <summary>获取量表显示名称。</summary>
    public required string DisplayName { get; init; }

    /// <summary>获取面向专业人员的用途说明。</summary>
    public required string Description { get; init; }

    /// <summary>获取量表正式分组与题序。</summary>
    public required IReadOnlyList<NutritionAssessmentSection> Sections { get; init; }

    /// <summary>按正式题序枚举量表中的全部题目。</summary>
    public IEnumerable<NutritionAssessmentItem> Items => Sections.SelectMany(section => section.Items);
}

/// <summary>
/// 表示量表开始时采用的评估对象快照。
/// </summary>
/// <remarks>
/// 快照避免咨询对象资料在作答过程中变化时静默改变既有量表结果。
/// </remarks>
public sealed record NutritionAssessmentSubject
{
    /// <summary>获取评估时的实足年龄（年）。</summary>
    public required int AgeInYears { get; init; }

    /// <summary>获取评估时的可选身高，单位为厘米。</summary>
    public decimal? HeightInCentimeters { get; init; }

    /// <summary>获取评估时的可选体重，单位为千克。</summary>
    public decimal? WeightInKilograms { get; init; }

    /// <summary>根据有效身高和体重计算 BMI；资料不足时为空。</summary>
    public decimal? BodyMassIndex
    {
        get
        {
            if (HeightInCentimeters is not > 0 || WeightInKilograms is not > 0)
            {
                return null;
            }

            var heightInMeters = HeightInCentimeters.Value / 100m;
            return WeightInKilograms.Value / heightInMeters / heightInMeters;
        }
    }
}

/// <summary>
/// 表示量表产生的一个具有稳定编码的分项结果。
/// </summary>
/// <param name="Code">分项结果编码。</param>
/// <param name="Display">分项结果名称。</param>
/// <param name="Value">分项结果数值。</param>
public sealed record NutritionAssessmentMetric(string Code, string Display, decimal Value);

/// <summary>
/// 表示一项量表解释对专业人员关注程度的领域语义。
/// </summary>
/// <remarks>
/// 本枚举不规定任何具体颜色或视觉样式；展示层负责将其映射为适合当前界面的提示。
/// </remarks>
public enum NutritionAssessmentAttentionLevel
{
    /// <summary>量表没有声明关注程度。</summary>
    Unspecified = 0,

    /// <summary>结果属于常规状态，无需额外强调。</summary>
    Routine = 1,

    /// <summary>结果需要专业人员关注或处理。</summary>
    RequiresAttention = 2
}

/// <summary>
/// 表示量表结果的编码化临床解释。
/// </summary>
/// <param name="Code">解释在当前量表版本内的稳定编码。</param>
/// <param name="Display">面向专业人员的解释文本。</param>
/// <param name="AttentionLevel">结果对专业人员关注程度的领域语义。</param>
public sealed record NutritionAssessmentInterpretation(
    string Code,
    string Display,
    NutritionAssessmentAttentionLevel AttentionLevel =
        NutritionAssessmentAttentionLevel.Unspecified);

/// <summary>
/// 表示当前回答经具体量表规则求得的状态和结果。
/// </summary>
public sealed record NutritionAssessmentEvaluation : ISoapContributor
{
    /// <summary>获取当前结果是否已经满足量表规定的完成条件。</summary>
    public required bool IsComplete { get; init; }

    /// <summary>获取当前作答状态下应当显示并保存的题目编码。</summary>
    public required IReadOnlySet<string> ApplicableItemCodes { get; init; }

    /// <summary>获取尚未回答的必需题目编码。</summary>
    public required IReadOnlyList<string> MissingItemCodes { get; init; }

    /// <summary>获取完整评估的总分；未完成或量表路径不产生总分时为空。</summary>
    public decimal? TotalScore { get; init; }

    /// <summary>获取具有解释价值的分项结果。</summary>
    public IReadOnlyList<NutritionAssessmentMetric> Metrics { get; init; } = [];

    /// <summary>获取完整评估的编码化解释。</summary>
    public NutritionAssessmentInterpretation? Interpretation { get; init; }

    /// <summary>获取供专业人员复核的 SOAP 候选文本。</summary>
    public SoapContribution SoapContribution { get; init; } = new();

    /// <inheritdoc />
    public SoapContribution ToSoapContribution() => SoapContribution;
}
