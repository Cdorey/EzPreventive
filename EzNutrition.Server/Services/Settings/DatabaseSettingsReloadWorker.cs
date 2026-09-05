namespace EzNutrition.Server.Services.Settings;

/// <summary>定期同步其他实例提交的配置；重载失败时保留上一份已验证的内存快照。</summary>
public sealed class DatabaseSettingsReloadWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<DatabaseSettingsReloadWorker> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30), timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                foreach (var loader in scope.ServiceProvider.GetServices<IDatabaseSettingsLoader>())
                {
                    try
                    {
                        await loader.ReloadAsync(stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception exception)
                    {
                        logger.LogError(exception, "重载配置组 {Key} 失败，将在下个周期重试。", loader.Key);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // 宿主停止时结束轮询。
        }
    }
}
