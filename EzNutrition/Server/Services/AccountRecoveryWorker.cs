namespace EzNutrition.Server.Services;

public sealed class AccountRecoveryWorker(
    AccountRecoveryQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<AccountRecoveryWorker> logger) : BackgroundService
{
    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(30);
    private const int ConsumerCount = 2;

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(Enumerable.Range(0, ConsumerCount).Select(_ => ConsumeAsync(stoppingToken)));

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var deliveryCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                deliveryCancellation.CancelAfter(DeliveryTimeout);
                await using var scope = scopeFactory.CreateAsyncScope();
                var accountSecurity = scope.ServiceProvider.GetRequiredService<AccountSecurityService>();
                switch (request.Kind)
                {
                    case AccountRecoveryRequestKind.EmailConfirmation:
                        await accountSecurity.RequestEmailConfirmationAsync(
                            request.Email,
                            deliveryCancellation.Token);
                        break;
                    case AccountRecoveryRequestKind.PasswordReset:
                        await accountSecurity.RequestPasswordResetAsync(
                            request.Email,
                            deliveryCancellation.Token);
                        break;
                    default:
                        logger.LogError("Unsupported account recovery request kind {Kind}.", request.Kind);
                        break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("An account recovery email job exceeded its delivery timeout.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An account recovery email job failed.");
            }
        }
    }
}
