using EzNutrition.Server.Data;
using EzNutrition.Server.Data.Entities;
using EzNutrition.Server.Services.Settings;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace EzNutrition.Server.Services.Maintenance;

/// <summary>按请求提交时间统计或批量删除过期的 LLM 审计记录。</summary>
public sealed class LlmAuditCleanupService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    TimeProvider timeProvider)
{
    /// <summary>处理请求时间严格早于截止时间的审计记录；未知请求时间不参与清理。</summary>
    /// <param name="cutoffUtc">必须是有效的当前或过去 UTC 截止时间。</param>
    /// <param name="dryRun">为 true 时只返回候选数量，不修改数据库。</param>
    /// <param name="cancellationToken">用于取消统计或数据库端删除。</param>
    public Task<LlmAuditCleanupResult> DeleteExpiredAsync(
        DateTimeOffset cutoffUtc,
        bool dryRun = true,
        CancellationToken cancellationToken = default) =>
        CleanupAsync(cutoffUtc, dryRun, null, cancellationToken);

    /// <summary>在可串行化事务内锁定扫描开始时的配置版本并删除过期记录。</summary>
    internal Task<LlmAuditCleanupResult> DeleteConfiguredExpiredAsync(
        DateTimeOffset cutoffUtc,
        Guid settingsVersion,
        CancellationToken cancellationToken = default)
    {
        if (settingsVersion == Guid.Empty)
        {
            throw new ArgumentException("配置版本不能使用空 GUID。", nameof(settingsVersion));
        }
        return CleanupAsync(cutoffUtc, false, settingsVersion, cancellationToken);
    }

    /// <summary>使用独立上下文执行统计或单条批量删除语句。</summary>
    private async Task<LlmAuditCleanupResult> CleanupAsync(
        DateTimeOffset cutoffUtc,
        bool dryRun,
        Guid? settingsVersion,
        CancellationToken cancellationToken)
    {
        if (cutoffUtc <= DateTimeOffset.MinValue || cutoffUtc > timeProvider.GetUtcNow())
        {
            throw new ArgumentOutOfRangeException(nameof(cutoffUtc), "截止时间必须是有效的当前或过去时间。");
        }
        if (System.Transactions.Transaction.Current is not null)
        {
            throw new InvalidOperationException("LLM 审计清理不能加入外部环境事务。");
        }

        cutoffUtc = cutoffUtc.ToUniversalTime();
        var cutoff = cutoffUtc.UtcDateTime;
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var expired = ExpiredRecords(db, cutoff);
        if (dryRun)
        {
            var matched = await expired.CountAsync(cancellationToken);
            return new(cutoffUtc, true, matched, 0);
        }

        if (settingsVersion is not { } expectedVersion)
        {
            var deleted = await expired.ExecuteDeleteAsync(cancellationToken);
            return new(cutoffUtc, false, deleted, deleted);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var currentVersion = await db.ApplicationSettings.AsNoTracking()
            .Where(setting => setting.Key == LlmAuditCleanupOptions.SectionName)
            .Select(setting => (Guid?)setting.Version)
            .SingleOrDefaultAsync(cancellationToken);
        if (currentVersion != expectedVersion)
        {
            return new(cutoffUtc, false, 0, 0, ConfigurationChanged: true);
        }

        var configuredDeleted = await expired.ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(cutoffUtc, false, configuredDeleted, configuredDeleted);
    }

    /// <summary>建立统一的过期条件，确保预览、手工执行和自动执行边界一致。</summary>
    private static IQueryable<PrescriptionGenerateRequest> ExpiredRecords(
        ApplicationDbContext db,
        DateTime cutoff) =>
        db.PrescriptionGenerateRequests.Where(request =>
            request.RequestTime > DateTime.MinValue && request.RequestTime < cutoff);
}
