namespace EzNutrition.Server.Services.Maintenance;

/// <summary>定期补偿清理失去数据库引用的证件文件和遗留的上传临时文件。</summary>
public sealed class OrphanFileCleanupWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<OrphanFileCleanupWorker> logger) : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(24);
    private static readonly TimeSpan FileGracePeriod = TimeSpan.FromHours(24);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CleanupInterval, timeProvider);
        try
        {
            do
            {
                try
                {
                    await CleanupAsync(stoppingToken);
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
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // 宿主停止时结束轮询；已经完成的文件删除保持生效。
        }
    }

    /// <summary>清理最后修改时间已超过安全宽限期的孤儿文件。</summary>
    internal async Task CleanupAsync(CancellationToken cancellationToken)
    {
        var cutoffUtc = timeProvider.GetUtcNow().ToUniversalTime() - FileGracePeriod;
        await using var scope = scopeFactory.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<OrphanCleanupService>()
            .DeleteOrphanedCertificateFilesAsync(cutoffUtc, cancellationToken);
        logger.LogInformation(
            "孤儿证件文件补偿清理完成：候选 {Candidates}，删除 {Deleted}，失败 {Failed}。",
            result.CleanupCandidates,
            result.DeletedFiles,
            result.FailedFiles);
    }
}
