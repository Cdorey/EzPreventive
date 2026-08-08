using EzNutrition.Archives.Contracts.Abstractions;
using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Archives.Contracts.Internal;
using EzNutrition.Archives.Contracts.Metadata;
using EzNutrition.Archives.Contracts.ValueObjects;

namespace EzNutrition.Archives.Contracts.Resources;

/// <summary>
/// 表示一个候选能量计算及其独立输入、参考数据和结果。
/// </summary>
public sealed record EnergyCalculationCandidate
{
    private IReadOnlyList<AssessmentInput> _inputs = Array.Empty<AssessmentInput>();
    private IReadOnlyList<ReferenceDataIdentity> _referenceData = Array.Empty<ReferenceDataIdentity>();
    private IReadOnlyList<NamedArchiveValue> _intermediateResults = Array.Empty<NamedArchiveValue>();

    /// <summary>
    /// 获取候选计算在本资源内的稳定标识。
    /// </summary>
    public required LocalIdentifier CandidateId { get; init; }

    /// <summary>
    /// 获取候选计算的方法和实现身份。
    /// </summary>
    public required AlgorithmIdentity Algorithm { get; init; }

    /// <summary>
    /// 获取该候选计算实际采用的完整输入。
    /// </summary>
    public IReadOnlyList<AssessmentInput> Inputs
    {
        get => _inputs;
        init => _inputs = ArchiveCollections.Freeze(value);
    }

    /// <summary>
    /// 获取该候选计算实际依赖的参考数据集。
    /// </summary>
    public IReadOnlyList<ReferenceDataIdentity> ReferenceData
    {
        get => _referenceData;
        init => _referenceData = ArchiveCollections.Freeze(value);
    }

    /// <summary>
    /// 获取候选总能量结果，单位应表达每日量纲。
    /// </summary>
    public required Quantity Result { get; init; }

    /// <summary>
    /// 获取具有临床解释价值的中间结果。
    /// </summary>
    public IReadOnlyList<NamedArchiveValue> IntermediateResults
    {
        get => _intermediateResults;
        init => _intermediateResults = ArchiveCollections.Freeze(value);
    }
}

/// <summary>
/// 表示医师对最终能量目标的专业决定。
/// </summary>
public sealed record ProfessionalEnergyDecision
{
    /// <summary>
    /// 获取医师最终采用的每日能量目标。
    /// </summary>
    public required Quantity AdoptedEnergyTarget { get; init; }

    /// <summary>
    /// 获取被直接采用或作为修正基础的候选计算标识。
    /// </summary>
    public LocalIdentifier? SelectedCandidateId { get; init; }

    /// <summary>
    /// 获取决定依据编码，例如公式结果、外部结果或纯临床判断；来源档案未提供时为空。
    /// </summary>
    public Coding? DecisionBasis { get; init; }

    /// <summary>
    /// 获取正式记录缺少决定依据时的明确原因。
    /// </summary>
    public DataAbsentReasonCode? DecisionBasisAbsentReason { get; init; }

    /// <summary>
    /// 获取专业修正或纯临床判断的说明。
    /// </summary>
    public string? Reason { get; init; }
}

/// <summary>
/// 表示能量需求候选计算和医师最终采用值。
/// </summary>
public sealed record EnergyAssessmentResource : IArchiveResource
{
    private IReadOnlyList<EnergyCalculationCandidate> _candidateCalculations =
        Array.Empty<EnergyCalculationCandidate>();

    /// <inheritdoc />
    public ResourceTypeCode ResourceType => ArchiveResourceTypes.EnergyAssessment;

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
    /// 获取候选能量计算。
    /// </summary>
    public IReadOnlyList<EnergyCalculationCandidate> CandidateCalculations
    {
        get => _candidateCalculations;
        init => _candidateCalculations = ArchiveCollections.Freeze(value);
    }

    /// <summary>
    /// 获取医师最终专业决定；尚未形成决定时为 <see langword="null"/>。
    /// </summary>
    public ProfessionalEnergyDecision? ProfessionalDecision { get; init; }

    /// <summary>
    /// 获取正式记录缺少专业决定时的明确原因。
    /// </summary>
    public DataAbsentReasonCode? ProfessionalDecisionAbsentReason { get; init; }
}
