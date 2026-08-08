using EzNutrition.Archives.Contracts.Abstractions;
using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Archives.Contracts.Internal;
using EzNutrition.Archives.Contracts.Metadata;
using EzNutrition.Archives.Contracts.ValueObjects;

namespace EzNutrition.Archives.Contracts.Resources;

/// <summary>
/// 表示 DRIs 人群组的基础选择、最终采用选择和调整说明。
/// </summary>
public sealed record PopulationGroupSelection
{
    /// <summary>
    /// 获取选择器自动或原始确定的人群组。
    /// </summary>
    public required Coding BasisGroup { get; init; }

    /// <summary>
    /// 获取评估实际采用的人群组。
    /// </summary>
    public required Coding AdoptedGroup { get; init; }

    /// <summary>
    /// 获取人群组发生实质调整时的专业理由。
    /// </summary>
    public string? AdjustmentReason { get; init; }
}

/// <summary>
/// 表示某种 DRIs 指标的历史参考值和可选专业调整。
/// </summary>
public sealed record DriReferenceValue
{
    /// <summary>
    /// 获取参考值类型编码，例如 EAR、RNI、AI、UL 或 AMDR。
    /// </summary>
    public required Coding ReferenceType { get; init; }

    /// <summary>
    /// 获取数据集原始提供的数量或范围。
    /// </summary>
    public ArchiveValue? BasisValue { get; init; }

    /// <summary>
    /// 获取评估实际采用的数量或范围。
    /// </summary>
    public ArchiveValue? AdoptedValue { get; init; }

    /// <summary>
    /// 获取参考值不存在时的明确缺失原因。
    /// </summary>
    public DataAbsentReasonCode? AbsentReason { get; init; }

    /// <summary>
    /// 获取原始值与采用值存在实质差异时的专业理由。
    /// </summary>
    public string? AdjustmentReason { get; init; }
}

/// <summary>
/// 表示一个营养素对应的全部膳食参考摄入量结果。
/// </summary>
public sealed record NutrientReferenceResult
{
    private IReadOnlyList<DriReferenceValue> _referenceValues = Array.Empty<DriReferenceValue>();

    /// <summary>
    /// 获取营养素的稳定编码。
    /// </summary>
    public required Coding Nutrient { get; init; }

    /// <summary>
    /// 获取该营养素的参考值集合。
    /// </summary>
    public IReadOnlyList<DriReferenceValue> ReferenceValues
    {
        get => _referenceValues;
        init => _referenceValues = ArchiveCollections.Freeze(value);
    }
}

/// <summary>
/// 表示一次膳食参考摄入量选择、快照和专业调整。
/// </summary>
public sealed record DriAssessmentResource : IArchiveResource
{
    private IReadOnlyList<AssessmentInput> _inputContext = Array.Empty<AssessmentInput>();
    private IReadOnlyList<NutrientReferenceResult> _nutrientResults = Array.Empty<NutrientReferenceResult>();

    /// <inheritdoc />
    public ResourceTypeCode ResourceType => ArchiveResourceTypes.DriAssessment;

    /// <inheritdoc />
    public required ResourceMetadata Metadata { get; init; }

    /// <summary>
    /// 获取咨询对象的逻辑资源引用。
    /// </summary>
    public required LogicalResourceReference SubjectReference { get; init; }

    /// <summary>
    /// 获取所属咨询的可选确切版本引用。
    /// </summary>
    public VersionedResourceReference? ConsultationReference { get; init; }

    /// <summary>
    /// 获取评估的临床有效时间。
    /// </summary>
    public required DateTimeOffset EffectiveAt { get; init; }

    /// <summary>
    /// 获取选择参考人群时实际采用的输入。
    /// </summary>
    public IReadOnlyList<AssessmentInput> InputContext
    {
        get => _inputContext;
        init => _inputContext = ArchiveCollections.Freeze(value);
    }

    /// <summary>
    /// 获取参考人群选择器的方法和实现身份；来源档案未提供时为空。
    /// </summary>
    public AlgorithmIdentity? Selector { get; init; }

    /// <summary>
    /// 获取本评估使用的单个 DRIs 数据集身份；来源档案未提供时为空。
    /// </summary>
    public ReferenceDataIdentity? ReferenceData { get; init; }

    /// <summary>
    /// 获取正式记录缺少 DRIs 数据集身份时的明确原因。
    /// </summary>
    public DataAbsentReasonCode? ReferenceDataAbsentReason { get; init; }

    /// <summary>
    /// 获取基础人群组和最终采用人群组；尚未完成选择时为空。
    /// </summary>
    public PopulationGroupSelection? PopulationGroup { get; init; }

    /// <summary>
    /// 获取评估当时实际使用的营养素参考值快照。
    /// </summary>
    public IReadOnlyList<NutrientReferenceResult> NutrientResults
    {
        get => _nutrientResults;
        init => _nutrientResults = ArchiveCollections.Freeze(value);
    }
}
