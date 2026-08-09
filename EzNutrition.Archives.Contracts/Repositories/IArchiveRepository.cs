using EzNutrition.Archives.Contracts.Abstractions;
using EzNutrition.Archives.Contracts.Identity;

namespace EzNutrition.Archives.Contracts.Repositories;

/// <summary>
/// 定义与文件、IndexedDB、SQLite 和医院数据库无关的类型化档案仓储。
/// </summary>
public interface IArchiveRepository
{
    /// <summary>
    /// 获取仓储支持的能力。
    /// </summary>
    ArchiveRepositoryCapabilities Capabilities { get; }

    /// <summary>
    /// 读取一个逻辑资源的全部当前版本头。
    /// </summary>
    /// <param name="reference">逻辑资源引用。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>当前版本头集合；资源不存在时集合为空。</returns>
    ValueTask<ArchiveCurrentResult> GetCurrentAsync(
        LogicalResourceReference reference,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 读取一个资源的确切历史版本。
    /// </summary>
    /// <param name="reference">确切资源版本引用。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>确切资源版本；不存在时为 <see langword="null"/>。</returns>
    ValueTask<IArchiveResource?> GetVersionAsync(
        VersionedResourceReference reference,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 按仓储可复现的顺序枚举一个逻辑资源的历史版本。
    /// </summary>
    /// <param name="reference">逻辑资源引用。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>资源历史版本异步序列。</returns>
    IAsyncEnumerable<IArchiveResource> GetHistoryAsync(
        LogicalResourceReference reference,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 原子保存一个变更集。
    /// </summary>
    /// <param name="changeSet">待保存资源和并发条件。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>整体保存结果。</returns>
    /// <remarks>
    /// 未声明 <see cref="ArchiveRepositoryCapabilities.AtomicChangeSets"/> 的仓储在收到多资源变更集时，
    /// 应在写入前返回能力错误；单资源变更仍按原子操作处理。
    /// </remarks>
    ValueTask<ArchiveCommitResult> SaveAsync(
        ArchiveChangeSet changeSet,
        CancellationToken cancellationToken = default);
}
