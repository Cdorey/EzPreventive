using EzNutrition.Archives.Contracts.Abstractions;
using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Archives.Contracts.Internal;
using EzNutrition.Archives.Contracts.ValueObjects;

namespace EzNutrition.Archives.Contracts.Bundles;

/// <summary>
/// 指定档案 Bundle 的用途。
/// </summary>
public enum ArchiveBundleType
{
    /// <summary>
    /// 不附加临床文档语义的一般资源集合。
    /// </summary>
    Collection = 0,

    /// <summary>
    /// 包含一次咨询及其引用闭包的咨询文档。
    /// </summary>
    ConsultationDocument = 1,

    /// <summary>
    /// 为导入、导出或迁移形成的资源包。
    /// </summary>
    TransferPackage = 2
}

/// <summary>
/// 表示一个或多个独立档案资源组成的传输容器。
/// </summary>
/// <remarks>
/// Bundle 内资源的物理顺序不决定资源修订关系，也不能替代确切资源引用。
/// </remarks>
public sealed record ArchiveBundle
{
    private IReadOnlyList<IArchiveResource> _entries = Array.Empty<IArchiveResource>();
    private IReadOnlyList<ArchiveExtension> _extensions = Array.Empty<ArchiveExtension>();

    /// <summary>
    /// 获取 Bundle 的全局标识。
    /// </summary>
    public required ArchiveBundleId BundleId { get; init; }

    /// <summary>
    /// 获取 Bundle 用途。
    /// </summary>
    public required ArchiveBundleType BundleType { get; init; }

    /// <summary>
    /// 获取 Bundle 建立时间。
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// 获取产生 Bundle 的应用身份。
    /// </summary>
    public required ApplicationIdentity Producer { get; init; }

    /// <summary>
    /// 获取 Bundle 中的完整资源快照。
    /// </summary>
    public IReadOnlyList<IArchiveResource> Entries
    {
        get => _entries;
        init => _entries = ArchiveCollections.Freeze(value);
    }

    /// <summary>
    /// 获取 Bundle 级扩展。
    /// </summary>
    public IReadOnlyList<ArchiveExtension> Extensions
    {
        get => _extensions;
        init => _extensions = ArchiveCollections.Freeze(value);
    }
}
