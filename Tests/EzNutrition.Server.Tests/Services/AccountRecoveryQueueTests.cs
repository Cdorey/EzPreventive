using EzNutrition.Server.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace EzNutrition.Server.Tests.Services;

public sealed class AccountRecoveryQueueTests
{
    [Fact]
    public async Task Same_email_and_kind_are_cooled_down_while_different_kinds_are_independent()
    {
        var queue = new AccountRecoveryQueue(NullLogger<AccountRecoveryQueue>.Instance);
        Assert.True(queue.TryEnqueue(AccountRecoveryRequestKind.EmailConfirmation, "  Person@Example.Test  "));
        Assert.True(queue.TryEnqueue(AccountRecoveryRequestKind.EmailConfirmation, "person@example.test"));
        Assert.True(queue.TryEnqueue(AccountRecoveryRequestKind.PasswordReset, "PERSON@EXAMPLE.TEST"));

        using var cancellation = new CancellationTokenSource();
        await using var reader = queue.ReadAllAsync(cancellation.Token).GetAsyncEnumerator();

        Assert.True(await reader.MoveNextAsync());
        var confirmation = reader.Current;
        Assert.Equal(AccountRecoveryRequestKind.EmailConfirmation, confirmation.Kind);
        Assert.Equal("Person@Example.Test", confirmation.Email);

        Assert.True(await reader.MoveNextAsync());
        var passwordReset = reader.Current;
        Assert.Equal(AccountRecoveryRequestKind.PasswordReset, passwordReset.Kind);
        Assert.Equal("PERSON@EXAMPLE.TEST", passwordReset.Email);

        cancellation.CancelAfter(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await reader.MoveNextAsync().AsTask());
    }

    [Fact]
    public async Task Full_queue_reports_that_concurrent_requests_were_not_admitted()
    {
        var queue = new AccountRecoveryQueue(NullLogger<AccountRecoveryQueue>.Instance);
        for (var index = 0; index < 256; index++)
        {
            Assert.True(queue.TryEnqueue(
                AccountRecoveryRequestKind.PasswordReset,
                $"person-{index}@example.test"));
        }

        var attempts = await Task.WhenAll(
            Enumerable.Range(0, 16).Select(_ => Task.Run(() => queue.TryEnqueue(
                AccountRecoveryRequestKind.PasswordReset,
                "overflow@example.test"))));

        Assert.All(attempts, Assert.False);
    }
}
