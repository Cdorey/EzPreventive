using EzNutrition.Server.Data;
using EzNutrition.Shared.Data.DTO;

namespace EzNutrition.Server.Services;

/// <summary>管理专业认证申请的审核记录，并在保存审核结果后清理证件图片。</summary>
/// <remarks>调用方负责授权；用户角色和声明由现有账号管理流程维护。</remarks>
public sealed class CertificationReviewService(
    ApplicationDbContext dbContext,
    CertificateFileStore certificateFileStore,
    TimeProvider timeProvider,
    ILogger<CertificationReviewService> logger)
{
    /// <summary>更新一条认证申请的审核信息。</summary>
    /// <remarks>
    /// 保持现有更新语义：允许设置任一已定义状态，每次更新均记录处理时间；提交时间不变。
    /// 通过或拒绝时清除图片引用，保存成功后尝试删除原图片；文件失败不会撤销审核结果。
    /// 应在独立工作单元中调用，不应包含待保存的无关变更或尚未提交的外部事务。
    /// </remarks>
    /// <param name="requestId">待更新的申请主键。</param>
    /// <param name="status">目标审核状态。</param>
    /// <param name="processDetails">处理结果或审核意见。</param>
    /// <param name="remarks">其他备注。</param>
    /// <param name="certificateTicket">仅在待审核状态下写入的图片引用。</param>
    /// <param name="cancellationToken">用于取消数据库操作的令牌。</param>
    /// <returns>更新结果及证件图片清理是否失败。</returns>
    public async Task<CertificationReviewResult> UpdateAsync(
        Guid requestId,
        RequestStatus status,
        string? processDetails,
        string? remarks,
        Guid? certificateTicket,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(status))
        {
            return new(CertificationReviewStatus.InvalidStatus);
        }

        var request = await dbContext.ProfessionalCertificationRequests.FindAsync(
            [requestId], cancellationToken);
        if (request is null)
        {
            logger.LogWarning("更新失败：认证请求 {RequestId} 不存在。", requestId);
            return new(CertificationReviewStatus.NotFound);
        }

        var previousTicket = request.CertificateTicket;
        request.Status = status;
        request.ProcessedTime = timeProvider.GetUtcNow().UtcDateTime;
        request.ProcessDetails = processDetails;
        request.Remarks = remarks;
        request.CertificateTicket = status == RequestStatus.Pending ? certificateTicket : null;
        await dbContext.SaveChangesAsync(cancellationToken);

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
                return new(CertificationReviewStatus.Updated, CertificateFileCleanupFailed: true);
            }
        }

        return new(CertificationReviewStatus.Updated);
    }
}
