using EzNutrition.Archives.Contracts.Abstractions;
using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Archives.Contracts.Internal;
using EzNutrition.Archives.Contracts.Metadata;
using EzNutrition.Archives.Contracts.ValueObjects;

namespace EzNutrition.Archives.Contracts.Resources;

/// <summary>
/// 表示一次量表评估所采用的量表定义身份。
/// </summary>
/// <remarks>
/// <see cref="Version"/>、<see cref="Definition"/> 的版本或
/// <see cref="DefinitionFingerprint"/> 至少应有一项能够定位确切定义，避免同名量表修订后无法解释历史回答。
/// 本类型只标识量表定义，不承载动态表单布局或具体评分规则。
/// </remarks>
public sealed record AssessmentInstrumentIdentity
{
    private string? _version;

    /// <summary>
    /// 获取量表的稳定编码；编码自身的版本只表示代码体系版本。
    /// </summary>
    public required Coding Code { get; init; }

    /// <summary>
    /// 获取量表自身的可选版次或发布版本。
    /// </summary>
    public string? Version
    {
        get => _version;
        init => _version = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// 获取量表定义及其可选版本的规范引用。
    /// </summary>
    public CanonicalReference? Definition { get; init; }

    /// <summary>
    /// 获取量表定义确切内容的可选指纹。
    /// </summary>
    public ContentFingerprint? DefinitionFingerprint { get; init; }
}

/// <summary>
/// 表示量表中一个稳定条目的回答及可选计分贡献。
/// </summary>
/// <remarks>
/// 条目显示文字不参与身份匹配；长期解释应以量表版本和条目编码为准。
/// 具体量表是否允许某种答案、是否跳题以及如何计分，由对应业务规则负责。
/// </remarks>
public sealed record AssessmentItemResponse
{
    /// <summary>
    /// 获取量表条目的稳定编码。
    /// </summary>
    public required Coding Item { get; init; }

    /// <summary>
    /// 获取类型化回答；无法取得回答时为空。
    /// </summary>
    public ArchiveValue? Answer { get; init; }

    /// <summary>
    /// 获取回答缺失时的明确原因。
    /// </summary>
    public DataAbsentReasonCode? AnswerAbsentReason { get; init; }

    /// <summary>
    /// 获取该回答按当次评分方法产生的可选分值贡献。
    /// </summary>
    public decimal? ScoreContribution { get; init; }
}

/// <summary>
/// 表示一次版本化营养量表评估的回答、评分结果和来源上下文。
/// </summary>
/// <remarks>
/// 本资源为结构相近的营养筛查或评估量表提供统一档案形状。NRS 2002、MNA 等具体量表应在
/// Domain 或 Application 中实现各自的录入和评分规则，再映射为本资源；新增量表不应要求
/// XML codec 认识新的 CLR 资源类型。对于无法由本结构忠实表达的复杂评估，仍可新增专用资源。
/// </remarks>
public sealed record NutritionScaleAssessmentResource : IArchiveResource
{
    private IReadOnlyList<VersionedResourceReference> _inputResourceReferences =
        Array.Empty<VersionedResourceReference>();
    private IReadOnlyList<AssessmentItemResponse> _responses = Array.Empty<AssessmentItemResponse>();
    private IReadOnlyList<NamedArchiveValue> _derivedResults = Array.Empty<NamedArchiveValue>();

    /// <inheritdoc />
    public ResourceTypeCode ResourceType => ArchiveResourceTypes.NutritionScaleAssessment;

    /// <inheritdoc />
    public required ResourceMetadata Metadata { get; init; }

    /// <summary>
    /// 获取评估对象的逻辑资源引用。
    /// </summary>
    public required LogicalResourceReference SubjectReference { get; init; }

    /// <summary>
    /// 获取评估所属咨询的可选确切版本引用。
    /// </summary>
    public VersionedResourceReference? ConsultationReference { get; init; }

    /// <summary>
    /// 获取评估在临床语义上生效或实施的时间。
    /// </summary>
    public required DateTimeOffset EffectiveAt { get; init; }

    /// <summary>
    /// 获取本次评估采用的确切量表身份。
    /// </summary>
    public required AssessmentInstrumentIdentity Instrument { get; init; }

    /// <summary>
    /// 获取形成回答或评分时直接采用的确切档案资源版本。
    /// </summary>
    public IReadOnlyList<VersionedResourceReference> InputResourceReferences
    {
        get => _inputResourceReferences;
        init => _inputResourceReferences = ArchiveCollections.Freeze(value);
    }

    /// <summary>
    /// 获取按稳定条目编码保存的回答快照。
    /// </summary>
    public IReadOnlyList<AssessmentItemResponse> Responses
    {
        get => _responses;
        init => _responses = ArchiveCollections.Freeze(value);
    }

    /// <summary>
    /// 获取评分方法产生的可选分项结果或具有解释价值的中间结果。
    /// </summary>
    public IReadOnlyList<NamedArchiveValue> DerivedResults
    {
        get => _derivedResults;
        init => _derivedResults = ArchiveCollections.Freeze(value);
    }

    /// <summary>
    /// 获取本次评估采用的可选评分方法及实现身份。
    /// </summary>
    public AlgorithmIdentity? ScoringMethod { get; init; }

    /// <summary>
    /// 获取可选总分；草稿或无法取得总分的来源记录可以为空。
    /// </summary>
    public decimal? TotalScore { get; init; }

    /// <summary>
    /// 获取正式记录无法提供总分时的明确原因。
    /// </summary>
    public DataAbsentReasonCode? TotalScoreAbsentReason { get; init; }

    /// <summary>
    /// 获取基于量表定义形成的可选结果解释或风险分类编码。
    /// </summary>
    public Coding? Interpretation { get; init; }

    /// <summary>
    /// 获取实施或记录本次量表评估的可选主体快照。
    /// </summary>
    /// <remarks>
    /// 该主体是历史行为事实，不表示其当前资质、角色或权限。
    /// </remarks>
    public ActorReference? Performer { get; init; }
}
