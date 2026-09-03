namespace EzNutrition.Server.Services.Maintenance;

/// <summary>定期清理过期会话，避免刷新记录无限增长。</summary>
public sealed class AuthenticationSessionCleanupWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<AuthenticationSessionCleanupWorker> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24), timeProvider);
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var deleted = await scope.ServiceProvider.GetRequiredService<AuthenticationSessionService>()
                    .DeleteExpiredAsync(stoppingToken);
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
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
