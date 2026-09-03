using EzNutrition.Server.Data;
using EzNutrition.Shared.Data.DTO;
using Microsoft.EntityFrameworkCore;

namespace EzNutrition.Server.Services;

/// <summary>以乐观并发方式更新认证申请，保存成功后清理证件图片。</summary>
/// <remarks>调用方负责授权；角色和声明由现有账号管理流程维护，不随审核冲突撤回。</remarks>
public sealed class CertificationReviewService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    CertificateFileStore certificateFileStore,
    TimeProvider timeProvider,
    ILogger<CertificationReviewService> logger)
{
    /// <summary>人工审批最多尝试三次保存；自动拒绝与备注编辑只尝试一次。</summary>
    private const int MaxSaveAttempts = 3;

    /// <summary>修改待审核申请的意见，或由管理员通过、拒绝申请，允许覆盖已有审核结果。</summary>
    /// <remarks>人工决定遇到并发变化时按最新记录有限重试；只编辑意见时保留待审核状态和原版本检查。</remarks>
    /// <param name="requestId">待更新的申请主键。</param>
    /// <param name="expectedVersion">调用方读取时的版本，不允许使用空 GUID。</param>
    /// <param name="status">目标状态；Pending 表示只修改意见和备注。</param>
    /// <param name="processDetails">处理结果或审核意见。</param>
    /// <param name="remarks">其他备注。</param>
    /// <param name="cancellationToken">用于取消数据库操作的令牌。</param>
    /// <returns>业务结果、保存后的版本及图片清理警告。</returns>
    public Task<CertificationReviewResult> UpdateAsync(
        Guid requestId, Guid expectedVersion, RequestStatus status,
        string? processDetails, string? remarks, CancellationToken cancellationToken = default) =>
        UpdateCoreAsync(requestId, expectedVersion, status, processDetails, remarks, null, cancellationToken);

    /// <summary>仅拒绝仍为初筛版本的超时待审核申请；版本变化或保存冲突时跳过，不重试。</summary>
    internal Task<CertificationReviewResult> RejectExpiredAsync(
        Guid requestId, Guid expectedVersion, DateTimeOffset cutoffUtc,
        CancellationToken cancellationToken = default) =>
        UpdateCoreAsync(requestId, expectedVersion, RequestStatus.Rejected,
            "申请超过待审核期限，系统自动拒绝。", null, cutoffUtc, cancellationToken);

    /// <summary>每次尝试使用独立 DbContext，避免旧跟踪实体或调用方无关变更参与保存。</summary>
    private async Task<CertificationReviewResult> UpdateCoreAsync(
        Guid requestId, Guid expectedVersion, RequestStatus status,
        string? processDetails, string? remarks, DateTimeOffset? cutoffUtc,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(status))
        {
            return new(CertificationReviewStatus.InvalidStatus);
        }
        if (expectedVersion == Guid.Empty)
        {
            return new(CertificationReviewStatus.InvalidVersion);
        }
        if (System.Transactions.Transaction.Current is not null)
        {
            throw new InvalidOperationException("认证审核不能加入外部环境事务，以免提交前删除证件图片。");
        }

        var requiresUnchangedPending = cutoffUtc is not null || status == RequestStatus.Pending;
        var maxAttempts = requiresUnchangedPending ? 1 : MaxSaveAttempts;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            await using var dbContext = await contextFactory.CreateDbContextAsync(cancellationToken);
            var request = await dbContext.ProfessionalCertificationRequests
                .SingleOrDefaultAsync(item => item.Id == requestId, cancellationToken);
            if (request is null)
            {
                return new(CertificationReviewStatus.NotFound);
            }
            if (requiresUnchangedPending &&
                (request.Status != RequestStatus.Pending || request.Version != expectedVersion))
            {
                return new(CertificationReviewStatus.Conflict);
            }
            if (cutoffUtc is { } cutoff &&
                (request.RequestTime <= DateTime.MinValue || request.RequestTime >= cutoff.UtcDateTime))
            {
                return new(CertificationReviewStatus.NotExpired);
            }

            var previousTicket = request.CertificateTicket;
            request.Status = status;
            request.ProcessedTime = status == RequestStatus.Pending ? null : timeProvider.GetUtcNow().UtcDateTime;
            request.ProcessDetails = processDetails;
            if (cutoffUtc is null)
            {
                request.Remarks = remarks;
            }
            if (status != RequestStatus.Pending)
            {
                request.CertificateTicket = null;
            }
            request.Version = Guid.NewGuid();
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (requiresUnchangedPending)
                {
                    return new(CertificationReviewStatus.Conflict);
                }
                // 人工决定可以重读后覆盖机器或其他人工的结果；失败的保存不清理图片。
                continue;
            }

            if (status != RequestStatus.Pending && previousTicket is not null)
            {
                try
                {
                    certificateFileStore.Delete(previousTicket.Value);
                }
                catch (Exception exception)
                {
                    logger.LogError(exception,
                        "认证请求 {RequestId} 已更新，但证件图片 {Ticket} 清理失败。", requestId, previousTicket);
                    return new(CertificationReviewStatus.Updated, CertificateFileCleanupFailed: true, Version: request.Version);
                }
            }
            return new(CertificationReviewStatus.Updated, Version: request.Version);
        }
        return new(CertificationReviewStatus.Conflict);
    }
}
