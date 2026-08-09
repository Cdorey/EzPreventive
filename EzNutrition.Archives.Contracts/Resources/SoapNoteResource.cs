using EzNutrition.Archives.Contracts.Abstractions;
using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Archives.Contracts.Metadata;

namespace EzNutrition.Archives.Contracts.Resources;

/// <summary>
/// 表示一次咨询中的主观、客观、评估和计划记录。
/// </summary>
public sealed record SoapNoteResource : IArchiveResource
{
    /// <inheritdoc />
    public ResourceTypeCode ResourceType => ArchiveResourceTypes.SoapNote;

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
    /// 获取记录的临床有效时间。
    /// </summary>
    public required DateTimeOffset EffectiveAt { get; init; }

    /// <summary>
    /// 获取主观资料；缺失时为 <see langword="null"/>。
    /// </summary>
    public string? Subjective { get; init; }

    /// <summary>
    /// 获取客观资料；缺失时为 <see langword="null"/>。
    /// </summary>
    public string? Objective { get; init; }

    /// <summary>
    /// 获取专业评估；缺失时为 <see langword="null"/>。
    /// </summary>
    public string? Assessment { get; init; }

    /// <summary>
    /// 获取处理或随访计划；缺失时为 <see langword="null"/>。
    /// </summary>
    public string? Plan { get; init; }
}
