using EzNutrition.Server.Services.Settings;

namespace EzNutrition.Server.Services.Maintenance;

/// <summary>按数据库配置扫描超时认证申请；配置未启用或未设置扫描间隔时不执行。</summary>
public sealed class CertificationRequestCleanupWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<CertificationRequestCleanupWorker> logger) : BackgroundService
{
    /// <summary>记录本实例最近一次扫描的开始时间；重启后按最新配置立即检查。</summary>
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
                    logger.LogError(exception, "扫描超时认证申请失败，将按配置重新检查。");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // 宿主停止时结束轮询；已经开始的一条申请由清理服务完成。
        }
    }

    /// <summary>每次检查直接读取持久化配置，不依赖可能滞后的 Options 内存快照。</summary>
    internal async Task ScanIfDueAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var settings = await scope.ServiceProvider
            .GetRequiredService<DatabaseSettings<CertificationRequestCleanupOptions>>().GetAsync(cancellationToken);
        var options = settings.Value;
        if (!options.AutoRejectEnabled || options.SweepIntervalHours is not { } intervalHours ||
            options.PendingTimeoutDays is not { } timeoutDays || settings.Version is not { } version)
        {
            lastScanStartedAtUtc = null;
            return;
        }
        var now = timeProvider.GetUtcNow();
        if (lastScanStartedAtUtc is { } last && (now - last).TotalHours < intervalHours)
        {
            return;
        }
        // 超过可表示历史范围的保留期没有候选，不进行日期减法或清理。
        if (timeoutDays >= (now - DateTimeOffset.MinValue).TotalDays)
        {
            return;
        }
        lastScanStartedAtUtc = now;
        var result = await scope.ServiceProvider.GetRequiredService<CertificationRequestCleanupService>()
            .RejectConfiguredExpiredAsync(now.AddDays(-timeoutDays), version, cancellationToken);
        if (result.ConfigurationChanged)
        {
            lastScanStartedAtUtc = null;
        }
        logger.LogInformation(
            "超时认证申请扫描完成：拒绝 {Rejected}，跳过 {Skipped}，失败 {Failed}，图片清理警告 {FileWarnings}，取消 {Canceled}，配置变化 {ConfigurationChanged}。",
            result.Items.Count(item => item.Status == CertificationRequestCleanupStatus.Rejected),
            result.Items.Count(item => item.Status == CertificationRequestCleanupStatus.Skipped),
            result.Items.Count(item => item.Status == CertificationRequestCleanupStatus.Failed),
            result.Items.Count(item => item.CertificateFileCleanupFailed), result.IsCanceled, result.ConfigurationChanged);
    }
}
