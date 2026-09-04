using EzNutrition.Server.Services.Settings;
using Microsoft.Extensions.Options;

namespace EzNutrition.Server.Services.Maintenance;

/// <summary>在配置的服务器本地时间统一执行站点每日清理任务。</summary>
/// <remarks>清理任务依次执行且相互隔离；单项失败不会阻止后续清理，宿主停止时取消剩余工作。</remarks>
public sealed class MaintenanceCleanupWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IOptionsMonitor<CleanupScheduleOptions> scheduleOptions,
    ILogger<MaintenanceCleanupWorker> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await WaitForNextStartTimeAsync(stoppingToken);
                await RunCleanupAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // 宿主停止时结束等待或当前清理轮次。
        }
    }

    /// <summary>等待下一个配置时间；配置热更新时取消当前等待并重新排程。</summary>
    private async Task WaitForNextStartTimeAsync(CancellationToken stoppingToken)
    {
        while (true)
        {
            var scheduleChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var subscription = scheduleOptions.OnChange(_ => scheduleChanged.TrySetResult());
            var nowUtc = timeProvider.GetUtcNow().ToUniversalTime();
            var nextStartUtc = CalculateNextStartUtc(
                nowUtc,
                scheduleOptions.CurrentValue.StartTime,
                timeProvider.LocalTimeZone);
            logger.LogInformation("下一轮站点清理计划于 {NextStartUtc} 开始。", nextStartUtc);

            using var delayCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var delay = Task.Delay(nextStartUtc - nowUtc, timeProvider, delayCancellation.Token);
            if (await Task.WhenAny(delay, scheduleChanged.Task) == delay)
            {
                await delay;
                return;
            }

            await delayCancellation.CancelAsync();
            try
            {
                await delay;
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                // 调度配置已变化，按新时间重新计算。
            }
        }
    }

    /// <summary>计算严格晚于当前时刻的下一次服务器本地启动时间。</summary>
    /// <remarks>夏令时跳过的本地时间顺延到第一个有效分钟；重复的本地时间选择第一次出现。</remarks>
    internal static DateTimeOffset CalculateNextStartUtc(
        DateTimeOffset nowUtc,
        TimeOnly startTime,
        TimeZoneInfo localTimeZone)
    {
        ArgumentNullException.ThrowIfNull(localTimeZone);
        nowUtc = nowUtc.ToUniversalTime();
        var localNow = TimeZoneInfo.ConvertTime(nowUtc, localTimeZone);
        var localStart = DateOnly.FromDateTime(localNow.DateTime)
            .ToDateTime(startTime, DateTimeKind.Unspecified);
        if (localStart <= localNow.DateTime)
        {
            localStart = localStart.AddDays(1);
        }
        while (localTimeZone.IsInvalidTime(localStart))
        {
            localStart = localStart.AddMinutes(1);
        }

        if (localTimeZone.IsAmbiguousTime(localStart))
        {
            var firstOffset = localTimeZone.GetAmbiguousTimeOffsets(localStart).Max();
            return new DateTimeOffset(localStart, firstOffset).ToUniversalTime();
        }
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localStart, localTimeZone));
    }

    /// <summary>依次执行一轮清理；各方法自行隔离并记录失败。</summary>
    private async Task RunCleanupAsync(CancellationToken stoppingToken)
    {
        await AuthenticationSessionCleanupAsync(stoppingToken);
        await CertificationRequestCleanupAsync(stoppingToken);
        await AccountCleanupAsync(stoppingToken);
        await OrphanFileCleanupAsync(stoppingToken);
        await LlmAuditCleanupAsync(stoppingToken);
    }

    /// <summary>删除已超过绝对期限或刷新空闲期限的登录会话。</summary>
    private async Task AuthenticationSessionCleanupAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var deleted = await scope.ServiceProvider.GetRequiredService<AuthenticationSessionService>().DeleteExpiredAsync(stoppingToken);
            logger.LogInformation("已清理 {Count} 个过期登录会话。", deleted);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "清理过期登录会话失败，将在下个周期重试。");
        }
    }

    /// <summary>根据当前配置拒绝超过待审核期限的认证申请。</summary>
    private async Task CertificationRequestCleanupAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var settings = await scope.ServiceProvider.GetRequiredService<DatabaseSettings<CertificationRequestCleanupOptions>>().GetAsync(stoppingToken);
            var options = settings.Value;
            if (!options.AutoRejectEnabled || options.PendingTimeoutDays is not { } timeoutDays || settings.Version is not { } version)
            {
                return;
            }
            var now = timeProvider.GetUtcNow();

            // 超过可表示历史范围的保留期没有候选，不进行日期减法或清理。
            if (timeoutDays >= (now - DateTimeOffset.MinValue).TotalDays)
            {
                return;
            }

            var result = await scope.ServiceProvider.GetRequiredService<CertificationRequestCleanupService>().RejectConfiguredExpiredAsync(now.AddDays(-timeoutDays), version, stoppingToken);

            logger.LogInformation(
                "超时认证申请扫描完成：拒绝 {Rejected}，跳过 {Skipped}，失败 {Failed}，图片清理警告 {FileWarnings}，取消 {Canceled}，配置变化 {ConfigurationChanged}。",
                result.Items.Count(item => item.Status == CertificationRequestCleanupStatus.Rejected),
                result.Items.Count(item => item.Status == CertificationRequestCleanupStatus.Skipped),
                result.Items.Count(item => item.Status == CertificationRequestCleanupStatus.Failed),
                result.Items.Count(item => item.CertificateFileCleanupFailed), result.IsCanceled, result.ConfigurationChanged);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "扫描超时认证申请失败，将在下个周期重试。");
        }
    }

    /// <summary>根据当前配置删除超过保留期限的 LLM 审计记录。</summary>
    private async Task LlmAuditCleanupAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var settings = await scope.ServiceProvider.GetRequiredService<DatabaseSettings<LlmAuditCleanupOptions>>().GetAsync(stoppingToken);
            var options = settings.Value;
            if (!options.Enabled || options.RetentionDays is not { } retentionDays || settings.Version is not { } version)
            {
                return;
            }

            var now = timeProvider.GetUtcNow().ToUniversalTime();

            var cutoffUtc = retentionDays >= (now - DateTimeOffset.MinValue).TotalDays
                ? DateTimeOffset.MinValue.AddTicks(1)
                : now.AddDays(-retentionDays);

            var result = await scope.ServiceProvider.GetRequiredService<LlmAuditCleanupService>().DeleteConfiguredExpiredAsync(cutoffUtc, version, stoppingToken);


            logger.LogInformation("LLM 审计记录清理完成：删除 {Deleted}，配置变化 {ConfigurationChanged}。", result.DeletedRecords, result.ConfigurationChanged);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "扫描过期 LLM 审计记录失败，将在下个周期重试。");
        }
    }

    /// <summary>删除超过安全宽限期的临时文件和孤儿证件文件。</summary>
    private async Task OrphanFileCleanupAsync(CancellationToken stoppingToken)
    {
        try
        {
            var cutoffUtc = timeProvider.GetUtcNow().ToUniversalTime() - TimeSpan.FromHours(24);
            await using var scope = scopeFactory.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<OrphanCleanupService>().DeleteOrphanedCertificateFilesAsync(cutoffUtc, stoppingToken);
            logger.LogInformation("孤儿证件文件补偿清理完成：候选 {Candidates}，删除 {Deleted}，失败 {Failed}。", result.CleanupCandidates, result.DeletedFiles, result.FailedFiles);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "孤儿证件文件清理失败，将在下个周期重试。");
        }
    }

    /// <summary>根据当前配置依次执行三条账号清理规则。</summary>
    private async Task AccountCleanupAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var settings = await scope.ServiceProvider
                .GetRequiredService<DatabaseSettings<AccountCleanupOptions>>().GetAsync(stoppingToken);
            var options = settings.Value;
            var hasEnabledRule = options.UnsubmittedCertificationCleanupEnabled || options.NonFormalAccountCleanupEnabled || options.InactiveFormalAccountCleanupEnabled;
            if (!hasEnabledRule || settings.Version is not { } version)
            {
                return;
            }

            var now = timeProvider.GetUtcNow().ToUniversalTime();

            var cleanup = scope.ServiceProvider.GetRequiredService<AccountCleanupService>();

            if (options.UnsubmittedCertificationCleanupEnabled)
            {
                var result = await cleanup.DeleteConfiguredAccountsWithoutRolesAsync(
                    CalculateCutoff(now, options.CertificationSubmissionGraceDays!.Value),
                    onlyWithoutApplications: true, version, stoppingToken);
                LogResult("从未申请认证", result);
            }

            if (options.NonFormalAccountCleanupEnabled)
            {
                var result = await cleanup.DeleteConfiguredAccountsWithoutRolesAsync(
                    CalculateCutoff(now, options.NonFormalAccountRetentionDays!.Value),
                    onlyWithoutApplications: false, version, stoppingToken);
                LogResult("无合法角色", result);
            }

            if (options.InactiveFormalAccountCleanupEnabled)
            {
                var result = await cleanup.DeleteConfiguredInactiveAccountsAsync(
                    CalculateCutoff(now, options.FormalAccountInactivityDays!.Value),
                    version, stoppingToken);
                LogResult("长期未登录", result);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "扫描待清理账号失败，将在下个周期重试。");
        }
    }

    /// <summary>计算保留期限对应的截止时间，并安全处理超过可表示范围的天数。</summary>
    private static DateTimeOffset CalculateCutoff(DateTimeOffset now, int retentionDays) =>
        retentionDays >= (now - DateTimeOffset.MinValue).TotalDays
            ? DateTimeOffset.MinValue.AddTicks(1)
            : now.AddDays(-retentionDays);

    /// <summary>记录单条账号清理规则的汇总结果，不写入账号身份信息。</summary>
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
