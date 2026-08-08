using EzNutrition.Archives.Contracts.Abstractions;
using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Archives.Contracts.Internal;

namespace EzNutrition.Archives.Contracts.Repositories;

/// <summary>
/// 表示档案变更集标识。
/// </summary>
public sealed record ArchiveChangeSetId
{
    /// <summary>
    /// 初始化变更集标识。
    /// </summary>
    /// <param name="value">非空 UUID。</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> 为空 UUID。</exception>
    public ArchiveChangeSetId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("变更集标识不能是空 UUID。", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// 获取 UUID 值。
    /// </summary>
    public Guid Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString("D");
}

/// <summary>
/// 指定保存资源时的并发前置条件。
/// </summary>
public enum ArchiveConcurrencyMode
{
    /// <summary>
    /// 不检查当前版本；只适用于明确允许覆盖竞争结果的非临床场景。
    /// </summary>
    Any = 0,

    /// <summary>
    /// 要求逻辑资源尚不存在。
    /// </summary>
    MustNotExist = 1,

    /// <summary>
    /// 要求当前版本与指定版本完全一致。
    /// </summary>
    MustMatchVersion = 2
}

/// <summary>
/// 表示一个逻辑资源的乐观并发条件。
/// </summary>
public sealed record ArchiveConcurrencyCondition
{
    /// <summary>
    /// 获取受条件约束的逻辑资源。
    /// </summary>
    public required LogicalResourceReference Resource { get; init; }

    /// <summary>
    /// 获取并发模式。
    /// </summary>
    public required ArchiveConcurrencyMode Mode { get; init; }

    /// <summary>
    /// 获取 <see cref="ArchiveConcurrencyMode.MustMatchVersion"/> 模式要求的当前版本。
    /// </summary>
    public ResourceVersionId? ExpectedVersionId { get; init; }
}

/// <summary>
/// 表示必须共同成功或共同失败的一组档案资源变更。
/// </summary>
public sealed record ArchiveChangeSet
{
    private IReadOnlyList<IArchiveResource> _resources = Array.Empty<IArchiveResource>();
    private IReadOnlyList<ArchiveConcurrencyCondition> _concurrencyConditions =
        Array.Empty<ArchiveConcurrencyCondition>();

    /// <summary>
    /// 获取变更集标识。
    /// </summary>
    public required ArchiveChangeSetId ChangeSetId { get; init; }

    /// <summary>
    /// 获取准备保存的资源版本。
    /// </summary>
    public IReadOnlyList<IArchiveResource> Resources
    {
        get => _resources;
        init => _resources = ArchiveCollections.Freeze(value);
    }

    /// <summary>
    /// 获取保存前必须满足的并发条件。
    /// </summary>
    public IReadOnlyList<ArchiveConcurrencyCondition> ConcurrencyConditions
    {
        get => _concurrencyConditions;
        init => _concurrencyConditions = ArchiveCollections.Freeze(value);
    }
}
