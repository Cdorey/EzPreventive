using EzNutrition.Server.Data;
using EzNutrition.Server.Services.Settings;
using EzNutrition.Shared.Data.DTO;
using Microsoft.EntityFrameworkCore;

namespace EzNutrition.Server.Services.Maintenance;

/// <summary>筛选超时待审核申请，逐条复核并复用统一审核流程，支持预览。</summary>
public sealed class CertificationRequestCleanupService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<CertificationRequestCleanupService> logger)
{
    /// <summary>拒绝提交时间严格早于截止时间的待审核申请；未知提交时间不参与处理。</summary>
    /// <remarks>手工调用由调用方授权，不受自动扫描开关限制；取消在申请之间生效。</remarks>
    /// <param name="cutoffUtc">本轮截止时间，必须是有效的当前或过去时间。</param>
    /// <param name="dryRun">为 true 时只返回候选，不修改数据库和文件。</param>
    /// <param name="cancellationToken">初筛可取消查询，执行阶段在申请之间停止。</param>
    public Task<CertificationRequestCleanupResult> RejectExpiredAsync(
        DateTimeOffset cutoffUtc, bool dryRun = true, CancellationToken cancellationToken = default) =>
        CleanupAsync(cutoffUtc, dryRun, null, cancellationToken);

    /// <summary>按扫描开始时的配置执行；每条申请开始前读取数据库，配置变化则停止本轮。</summary>
    internal Task<CertificationRequestCleanupResult> RejectConfiguredExpiredAsync(
        DateTimeOffset cutoffUtc, Guid settingsVersion, CancellationToken cancellationToken) =>
        CleanupAsync(cutoffUtc, false, settingsVersion, cancellationToken);

    /// <summary>冻结初筛清单；执行时由审核服务重新检查状态、期限和并发版本。</summary>
    private async Task<CertificationRequestCleanupResult> CleanupAsync(
        DateTimeOffset cutoffUtc, bool dryRun, Guid? settingsVersion, CancellationToken cancellationToken)
    {
        if (cutoffUtc <= DateTimeOffset.MinValue || cutoffUtc > timeProvider.GetUtcNow())
        {
            throw new ArgumentOutOfRangeException(nameof(cutoffUtc), "截止时间必须是有效的当前或过去时间。");
        }
        if (System.Transactions.Transaction.Current is not null)
        {
            throw new InvalidOperationException("超时拒绝不能加入外部环境事务。");
        }
        cutoffUtc = cutoffUtc.ToUniversalTime();
        var cutoff = cutoffUtc.UtcDateTime;
        List<CertificationRequestCleanupItem> items;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            items = await db.ProfessionalCertificationRequests.AsNoTracking()
                .Where(request => request.Status == RequestStatus.Pending &&
                    request.RequestTime > DateTime.MinValue && request.RequestTime < cutoff)
                .OrderBy(request => request.RequestTime).ThenBy(request => request.Id)
                .Select(request => new CertificationRequestCleanupItem(
                    request.Id, request.UserId, request.RequestTime, request.Version,
                    CertificationRequestCleanupStatus.WouldReject, "待审核且提交时间早于截止时间。", false))
                .ToListAsync(cancellationToken);
        }
        if (dryRun)
        {
            return new(cutoffUtc, true, items);
        }

        for (var index = 0; index < items.Count; index++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return new(cutoffUtc, false, items, IsCanceled: true);
            }
            await using var scope = scopeFactory.CreateAsyncScope();
            if (settingsVersion is { } expectedSettingsVersion)
            {
                // 配置读取失败时整轮停止并向上报告，不沿用内存中的旧配置继续执行。
                var settings = await scope.ServiceProvider
                    .GetRequiredService<DatabaseSettings<CertificationRequestCleanupOptions>>().GetAsync();
                if (settings.Version != expectedSettingsVersion || !settings.Value.AutoRejectEnabled)
                {
                    return new(cutoffUtc, false, items, ConfigurationChanged: true);
                }
            }
            var item = items[index];
            try
            {
                // 当前申请一旦开始，就完成单次保存尝试及成功后的图片清理，再响应本轮取消。
                var result = await scope.ServiceProvider.GetRequiredService<CertificationReviewService>()
                    .RejectExpiredAsync(item.RequestId, item.Version, cutoffUtc);
                items[index] = result.Status switch
                {
                    CertificationReviewStatus.Updated => item with
                    {
                        Status = CertificationRequestCleanupStatus.Rejected,
                        Reason = result.CertificateFileCleanupFailed ? "已拒绝，证件图片需后续补偿清理。" : "已拒绝超时申请。",
                        CertificateFileCleanupFailed = result.CertificateFileCleanupFailed
                    },
                    CertificationReviewStatus.NotFound => item with
                    { Status = CertificationRequestCleanupStatus.Skipped, Reason = "申请已不存在。" },
                    CertificationReviewStatus.Conflict => item with
                    { Status = CertificationRequestCleanupStatus.Skipped, Reason = "申请状态或版本已变化，本轮跳过。" },
                    CertificationReviewStatus.NotExpired => item with
                    { Status = CertificationRequestCleanupStatus.Skipped, Reason = "申请已不满足超时条件。" },
                    _ => item with
                    { Status = CertificationRequestCleanupStatus.Failed, Reason = "申请数据未通过审核校验。" }
                };
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "拒绝超时认证申请 {RequestId} 失败。", item.RequestId);
                items[index] = item with
                { Status = CertificationRequestCleanupStatus.Failed, Reason = "未能确认处理成功，详情见服务端日志。" };
            }
        }
        return new(cutoffUtc, false, items);
    }
}
