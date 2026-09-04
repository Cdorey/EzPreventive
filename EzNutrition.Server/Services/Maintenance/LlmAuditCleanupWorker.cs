using EzNutrition.Server.Services.Settings;

namespace EzNutrition.Server.Services.Maintenance;

/// <summary>按数据库配置定期删除超过保留期限的 LLM 审计记录。</summary>
public sealed class LlmAuditCleanupWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<LlmAuditCleanupWorker> logger) : BackgroundService
{
    /// <summary>记录本实例最近一次扫描的开始时间；重启或重新启用后立即检查。</summary>
    private DateTimeOffset? lastScanStartedAtUtc;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), timeProvider);
        try
        {
            do
            {
                try
                {
                    await ScanIfDueAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "扫描过期 LLM 审计记录失败，将按配置重新检查。");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // 宿主停止时结束轮询；已经提交的批量删除保持完成。
        }
    }

    /// <summary>读取最新持久化配置，并在扫描到期时执行数据库端批量删除。</summary>
    internal async Task ScanIfDueAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var settings = await scope.ServiceProvider
            .GetRequiredService<DatabaseSettings<LlmAuditCleanupOptions>>().GetAsync(cancellationToken);
        var options = settings.Value;
        if (!options.Enabled || options.RetentionDays is not { } retentionDays ||
            options.SweepIntervalHours is not { } intervalHours || settings.Version is not { } version)
        {
            lastScanStartedAtUtc = null;
            return;
        }

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        if (lastScanStartedAtUtc is { } last && (now - last).TotalHours < intervalHours)
        {
            return;
        }
        lastScanStartedAtUtc = now;
        var cutoffUtc = retentionDays >= (now - DateTimeOffset.MinValue).TotalDays
            ? DateTimeOffset.MinValue.AddTicks(1)
            : now.AddDays(-retentionDays);
        var result = await scope.ServiceProvider.GetRequiredService<LlmAuditCleanupService>()
            .DeleteConfiguredExpiredAsync(cutoffUtc, version, cancellationToken);
        if (result.ConfigurationChanged)
        {
            lastScanStartedAtUtc = null;
        }
        logger.LogInformation(
            "LLM 审计记录清理完成：删除 {Deleted}，配置变化 {ConfigurationChanged}。",
            result.DeletedRecords,
            result.ConfigurationChanged);
    }
}
