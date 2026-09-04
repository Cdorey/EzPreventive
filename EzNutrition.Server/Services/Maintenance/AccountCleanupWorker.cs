using EzNutrition.Server.Services.Settings;

namespace EzNutrition.Server.Services.Maintenance;

/// <summary>按数据库配置依次清理未申请认证、无角色和长期未登录的账号。</summary>
public sealed class AccountCleanupWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<AccountCleanupWorker> logger) : BackgroundService
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
                    logger.LogError(exception, "扫描待清理账号失败，将按配置重新检查。");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // 宿主停止时结束轮询；已经开始的单个账号仍由清理服务完成。
        }
    }

    /// <summary>读取最新持久化配置，并在扫描到期时依次执行已启用规则。</summary>
    internal async Task ScanIfDueAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var settings = await scope.ServiceProvider
            .GetRequiredService<DatabaseSettings<AccountCleanupOptions>>().GetAsync(cancellationToken);
        var options = settings.Value;
        var hasEnabledRule = options.UnsubmittedCertificationCleanupEnabled ||
            options.NonFormalAccountCleanupEnabled || options.InactiveFormalAccountCleanupEnabled;
        if (!hasEnabledRule || options.SweepIntervalHours is not { } intervalHours ||
            settings.Version is not { } version)
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
        var cleanup = scope.ServiceProvider.GetRequiredService<AccountCleanupService>();

        if (options.UnsubmittedCertificationCleanupEnabled)
        {
            var result = await cleanup.DeleteConfiguredAccountsWithoutRolesAsync(
                CalculateCutoff(now, options.CertificationSubmissionGraceDays!.Value),
                onlyWithoutApplications: true, version, cancellationToken);
            LogResult("从未申请认证", result);
            if (StopAfter(result))
            {
                return;
            }
        }

        if (options.NonFormalAccountCleanupEnabled)
        {
            var result = await cleanup.DeleteConfiguredAccountsWithoutRolesAsync(
                CalculateCutoff(now, options.NonFormalAccountRetentionDays!.Value),
                onlyWithoutApplications: false, version, cancellationToken);
            LogResult("无合法角色", result);
            if (StopAfter(result))
            {
                return;
            }
        }

        if (options.InactiveFormalAccountCleanupEnabled)
        {
            var result = await cleanup.DeleteConfiguredInactiveAccountsAsync(
                CalculateCutoff(now, options.FormalAccountInactivityDays!.Value),
                version, cancellationToken);
            LogResult("长期未登录", result);
            StopAfter(result);
        }
    }

    /// <summary>配置变化时允许下一次轮询立即按新配置执行；取消则直接结束本轮。</summary>
    private bool StopAfter(AccountCleanupResult result)
    {
        if (result.ConfigurationChanged)
        {
            lastScanStartedAtUtc = null;
        }
        return result.ConfigurationChanged || result.IsCanceled;
    }

    /// <summary>计算保留期限对应的截止时间，并安全处理超过可表示范围的天数。</summary>
    private static DateTimeOffset CalculateCutoff(DateTimeOffset now, int retentionDays) =>
        retentionDays >= (now - DateTimeOffset.MinValue).TotalDays
            ? DateTimeOffset.MinValue.AddTicks(1)
            : now.AddDays(-retentionDays);

    /// <summary>记录单条规则的可观测结果，不包含账号身份信息。</summary>
    private void LogResult(string rule, AccountCleanupResult result)
    {
        logger.LogInformation(
            "账号清理规则 {Rule} 执行完成：删除 {Deleted}，跳过 {Skipped}，失败 {Failed}，文件警告 {FileWarnings}，取消 {Canceled}，配置变化 {ConfigurationChanged}。",
            rule,
            result.Items.Count(item => item.Status == AccountCleanupStatus.Deleted),
            result.Items.Count(item => item.Status == AccountCleanupStatus.Skipped),
            result.Items.Count(item => item.Status == AccountCleanupStatus.Failed),
            result.Items.Sum(item => item.CertificateFileCleanupFailures),
            result.IsCanceled,
            result.ConfigurationChanged);
    }
}
