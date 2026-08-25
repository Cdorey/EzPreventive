using EzNutrition.Archives.Contracts.Abstractions;
using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Archives.Contracts.Internal;
using EzNutrition.Archives.Contracts.Metadata;
using EzNutrition.Archives.Contracts.ValueObjects;

namespace EzNutrition.Archives.Contracts.Resources;

/// <summary>
/// 指定营养建议生成过程的状态。
/// </summary>
public enum NutritionAdviceGenerationStatus
{
    /// <summary>已准备输入，尚未开始生成。</summary>
    Prepared = 0,

    /// <summary>正在生成。</summary>
    Generating = 1,

    /// <summary>已获得完整建议。</summary>
    Completed = 2,

    /// <summary>生成中断，当前内容可能不完整。</summary>
    Incomplete = 3,

    /// <summary>生成失败。</summary>
    Failed = 4
}

/// <summary>
/// 表示供专业人员复核的营养建议及其生成上下文。
/// </summary>
public sealed record NutritionAdviceResource : IArchiveResource
{
    private IReadOnlyList<VersionedResourceReference> _inputResourceReferences =
        Array.Empty<VersionedResourceReference>();
    private IReadOnlyList<NamedArchiveValue> _inputSummary = Array.Empty<NamedArchiveValue>();

    /// <inheritdoc />
    public ResourceTypeCode ResourceType => ArchiveResourceTypes.NutritionAdvice;

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
    /// 获取生成状态。
    /// </summary>
    public required NutritionAdviceGenerationStatus GenerationStatus { get; init; }

    /// <summary>
    /// 获取请求生成的时间。
    /// </summary>
    public DateTimeOffset? RequestedAt { get; init; }

    /// <summary>
    /// 获取生成完成或中断的时间。
    /// </summary>
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>
    /// 获取生成器身份。
    /// </summary>
    public AlgorithmIdentity? Generator { get; init; }

    /// <summary>
    /// 获取生成平台、模型或部署环境的补充说明。
    /// </summary>
    public string? GeneratorDetails { get; init; }

    /// <summary>
    /// 获取参与建议生成的确切资源版本。
    /// </summary>
    public IReadOnlyList<VersionedResourceReference> InputResourceReferences
    {
        get => _inputResourceReferences;
        init => _inputResourceReferences = ArchiveCollections.Freeze(value);
    }

    /// <summary>
    /// 获取发送给生成器的结构化输入摘要。
    /// </summary>
    public IReadOnlyList<NamedArchiveValue> InputSummary
    {
        get => _inputSummary;
        init => _inputSummary = ArchiveCollections.Freeze(value);
    }

    /// <summary>
    /// 获取生成器返回的分析过程文本。
    /// </summary>
    public string? ReasoningContent { get; init; }

    /// <summary>
    /// 获取生成器返回并供专业人员复核的建议正文。
    /// </summary>
    public string? NarrativeContent { get; init; }
}
