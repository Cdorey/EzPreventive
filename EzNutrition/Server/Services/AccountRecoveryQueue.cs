using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;

namespace EzNutrition.Server.Services;

public interface IAccountRecoveryQueue
{
    bool TryEnqueue(AccountRecoveryRequestKind kind, string email);
}

public enum AccountRecoveryRequestKind
{
    EmailConfirmation,
    PasswordReset
}

public sealed record AccountRecoveryRequest(
    AccountRecoveryRequestKind Kind,
    string Email);

public sealed class AccountRecoveryQueue(ILogger<AccountRecoveryQueue> logger)
    : IAccountRecoveryQueue
{
    private static readonly TimeSpan EmailCooldown = TimeSpan.FromMinutes(1);
    private const int CooldownTrimThreshold = 1024;
    private const int MaxCooldownEntries = 4096;

    private readonly Channel<AccountRecoveryRequest> channel = Channel.CreateBounded<AccountRecoveryRequest>(
        new BoundedChannelOptions(256)
        {
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });
    private readonly object admissionGate = new();
    private readonly Dictionary<string, DateTimeOffset> cooldowns = new(StringComparer.Ordinal);

    public bool TryEnqueue(AccountRecoveryRequestKind kind, string email)
    {
        var now = DateTimeOffset.UtcNow;
        var key = CreateCooldownKey(kind, email);
        lock (admissionGate)
        {
            if (cooldowns.TryGetValue(key, out var nextAllowed) && nextAllowed > now)
            {
                return true;
            }

            if (cooldowns.Count >= MaxCooldownEntries)
            {
                TrimExpiredCooldowns(now, MaxCooldownEntries);
                if (cooldowns.Count >= MaxCooldownEntries)
                {
                    logger.LogWarning("The account recovery cooldown cache is full; a request was not scheduled.");
                    return false;
                }
            }

            if (!channel.Writer.TryWrite(new AccountRecoveryRequest(kind, email.Trim())))
            {
                logger.LogWarning("The account recovery queue is full; a request was not scheduled.");
                return false;
            }

            cooldowns[key] = now.Add(EmailCooldown);
            TrimExpiredCooldowns(now, 128);
            return true;
        }
    }

    internal IAsyncEnumerable<AccountRecoveryRequest> ReadAllAsync(
        CancellationToken cancellationToken) =>
        channel.Reader.ReadAllAsync(cancellationToken);

    private void TrimExpiredCooldowns(DateTimeOffset now, int limit)
    {
        if (cooldowns.Count <= CooldownTrimThreshold)
        {
            return;
        }

        var expiredKeys = cooldowns
            .Where(item => item.Value <= now)
            .Select(item => item.Key)
            .Take(limit)
            .ToArray();
        foreach (var key in expiredKeys)
        {
            cooldowns.Remove(key);
        }
    }

    private static string CreateCooldownKey(AccountRecoveryRequestKind kind, string email)
    {
        var normalizedEmail = email.Trim().ToUpperInvariant();
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedEmail));
        return $"{kind}:{Convert.ToHexString(digest)}";
    }
}
