using EzNutrition.Archives.Contracts.Abstractions;
using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Archives.Contracts.Internal;
using EzNutrition.Archives.Contracts.Validation;

namespace EzNutrition.Archives.Contracts.Repositories;

/// <summary>
/// 指定档案仓储声明支持的能力。
/// </summary>
[Flags]
public enum ArchiveRepositoryCapabilities
{
    /// <summary>
    /// 未声明可选能力。
    /// </summary>
    None = 0,

    /// <summary>
    /// 可以读取历史确切版本。
    /// </summary>
    VersionHistory = 1,

    /// <summary>
    /// 可以原子保存包含多个资源的变更集。
    /// </summary>
    AtomicChangeSets = 2,

    /// <summary>
    /// 可以执行乐观并发条件。
    /// </summary>
    OptimisticConcurrency = 4
}

/// <summary>
/// 表示一个逻辑资源的当前版本头集合。
/// </summary>
public sealed record ArchiveCurrentResult
{
    private IReadOnlyList<IArchiveResource> _heads = Array.Empty<IArchiveResource>();

    /// <summary>
    /// 获取当前版本头；线性历史通常包含零个或一个元素，分支历史可以包含多个元素。
    /// </summary>
    /// <exception cref="ArgumentException">集合包含空资源、多个逻辑资源、多个资源类型或重复版本。</exception>
    public IReadOnlyList<IArchiveResource> Heads
    {
        get => _heads;
        init => _heads = FreezeHeads(value);
    }

    /// <summary>
    /// 获取逻辑资源是否存在。
    /// </summary>
    public bool IsFound => Heads.Count > 0;

    /// <summary>
    /// 获取历史是否具有多个当前版本头。
    /// </summary>
    public bool HasConflict => Heads.Count > 1;

    private static IReadOnlyList<IArchiveResource> FreezeHeads(IEnumerable<IArchiveResource> source)
    {
        var heads = ArchiveCollections.Freeze(source);
        if (heads.Count == 0)
        {
            return heads;
        }

        if (heads.Any(static head => head is null))
        {
            throw new ArgumentException("当前版本头集合不能包含空资源。", nameof(source));
        }

        var first = heads[0];
        if (heads.Any(head =>
                head.Metadata.ResourceId != first.Metadata.ResourceId ||
                head.ResourceType != first.ResourceType))
        {
            throw new ArgumentException("当前版本头必须属于同一逻辑资源和资源类型。", nameof(source));
        }

        if (heads.Select(static head => head.Metadata.VersionId).Distinct().Count() != heads.Count)
        {
            throw new ArgumentException("当前版本头不能包含重复的资源版本。", nameof(source));
        }

        return heads;
    }
}

/// <summary>
/// 表示一次档案变更集保存结果。
/// </summary>
public sealed record ArchiveCommitResult
{
    private IReadOnlyList<VersionedResourceReference> _savedResources =
        Array.Empty<VersionedResourceReference>();
    private IReadOnlyList<ArchiveValidationIssue> _issues = Array.Empty<ArchiveValidationIssue>();

    /// <summary>
    /// 获取保存是否整体成功。
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// 获取成功保存的确切资源版本引用。
    /// </summary>
    public IReadOnlyList<VersionedResourceReference> SavedResources
    {
        get => _savedResources;
        init => _savedResources = ArchiveCollections.Freeze(value);
    }

    /// <summary>
    /// 获取校验、能力或并发问题。
    /// </summary>
    public IReadOnlyList<ArchiveValidationIssue> Issues
    {
        get => _issues;
        init => _issues = ArchiveCollections.Freeze(value);
    }
}
