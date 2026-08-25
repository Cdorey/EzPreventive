using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Archives.Contracts.Internal;
using EzNutrition.Archives.Contracts.ValueObjects;

namespace EzNutrition.Archives.Contracts.Metadata;

/// <summary>
/// 表示所有档案资源共享的身份、版本、生命周期和来源元数据。
/// </summary>
public sealed record ResourceMetadata
{
    private IReadOnlyList<ArchiveExtension> _extensions = Array.Empty<ArchiveExtension>();

    /// <summary>
    /// 获取跨版本保持不变的逻辑资源标识。
    /// </summary>
    public required ResourceId ResourceId { get; init; }

    /// <summary>
    /// 获取当前资源版本的独立标识。
    /// </summary>
    public required ResourceVersionId VersionId { get; init; }

    /// <summary>
    /// 获取供人阅读和一致性检查使用的修订序号。
    /// </summary>
    public required RevisionNumber RevisionNumber { get; init; }

    /// <summary>
    /// 获取资源生命周期状态。
    /// </summary>
    public required ResourceLifecycleStatus Status { get; init; }

    /// <summary>
    /// 获取记录被标记为错误建立时的原因编码。
    /// </summary>
    public Coding? EnteredInErrorReason { get; init; }

    /// <summary>
    /// 获取记录被标记为错误建立时的补充说明。
    /// </summary>
    public string? EnteredInErrorReasonText { get; init; }

    /// <summary>
    /// 获取记录被标记为错误建立的时间。
    /// </summary>
    public DateTimeOffset? EnteredInErrorAt { get; init; }

    /// <summary>
    /// 获取执行错误标记的人员或机构。
    /// </summary>
    public ActorReference? EnteredInErrorBy { get; init; }

    /// <summary>
    /// 获取当前资源版本的建立时间。
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// 获取当前资源版本最后发生内容变更的时间。
    /// </summary>
    /// <remarks>
    /// 该时间不能替代 <see cref="Supersedes"/> 判断正式版本的先后关系。
    /// </remarks>
    public required DateTimeOffset LastModifiedAt { get; init; }

    /// <summary>
    /// 获取正式确认时间；草稿通常为 <see langword="null"/>。
    /// </summary>
    public DateTimeOffset? FinalizedAt { get; init; }

    /// <summary>
    /// 获取正式确认者；无法确认时应使用明确的缺失原因。
    /// </summary>
    public ActorReference? FinalizedBy { get; init; }

    /// <summary>
    /// 获取当前草稿所依据的确切资源版本。
    /// </summary>
    public VersionedResourceReference? BasedOn { get; init; }

    /// <summary>
    /// 获取被当前正式修订明确替代的确切资源版本。
    /// </summary>
    public VersionedResourceReference? Supersedes { get; init; }

    /// <summary>
    /// 获取创建当前资源版本的应用身份。
    /// </summary>
    public required ApplicationIdentity SourceApplication { get; init; }

    /// <summary>
    /// 获取不改变核心字段含义的扩展。
    /// </summary>
    public IReadOnlyList<ArchiveExtension> Extensions
    {
        get => _extensions;
        init => _extensions = ArchiveCollections.Freeze(value);
    }
}
