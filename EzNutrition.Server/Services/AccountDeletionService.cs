using EzNutrition.Server.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EzNutrition.Server.Services;

public enum AccountDeletionReason
{
    AdministratorRequested,
    UserRequested,
    RegistrationRollback,
    UnconfirmedEmailExpired,
    ProfessionalCertificationExpired,
    InactiveAccountExpired
}

public sealed record AccountDeletionResult(
    bool AccountFound,
    bool Succeeded,
    int DeletedAiAuditRecords,
    int DeletedCertificationRequests,
    int CertificateFileCleanupAttempts,
    int CertificateFileCleanupFailures,
    IReadOnlyList<IdentityError> IdentityErrors);

public sealed class AccountDeletionService(
    ApplicationDbContext applicationDb,
    UserManager<IdentityUser> userManager,
    CertificateFileStore certificateFileStore,
    ILogger<AccountDeletionService> logger)
{
    public async Task<AccountDeletionResult> DeleteAsync(
        string userId,
        AccountDeletionReason reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown account deletion reason.");
        }

        var operationId = Guid.NewGuid();
        var user = await userManager.FindByIdAsync(userId);
        var accountFound = user is not null;

        var certificateTickets = await applicationDb.ProfessionalCertificationRequests
            .AsNoTracking()
            .Where(request => request.UserId == userId && request.CertificateTicket != null)
            .Select(request => request.CertificateTicket!.Value)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        var deletedAiAudits = 0;
        var deletedCertificationRequests = 0;
        await using (var transaction = await applicationDb.Database.BeginTransactionAsync(cancellationToken))
        {
            deletedAiAudits = await applicationDb.PrescriptionGenerateRequests
                .Where(request => request.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);
            deletedCertificationRequests = await applicationDb.ProfessionalCertificationRequests
                .Where(request => request.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            if (user is not null)
            {
                var identityResult = await userManager.DeleteAsync(user);
                if (!identityResult.Succeeded)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    applicationDb.ChangeTracker.Clear();
                    var errors = identityResult.Errors.ToArray();
                    logger.LogWarning(
                        "Account deletion operation {OperationId} for reason {Reason} was rolled back because Identity returned {ErrorCodes}.",
                        operationId,
                        reason,
                        string.Join(",", errors.Select(error => error.Code)));
                    return new AccountDeletionResult(true, false, 0, 0, 0, 0, errors);
                }
            }

            await transaction.CommitAsync(cancellationToken);
        }

        applicationDb.ChangeTracker.Clear();
        var certificateFileCleanupAttempts = 0;
        var certificateFileCleanupFailures = 0;
        foreach (var certificateTicket in certificateTickets)
        {
            certificateFileCleanupAttempts++;
            try
            {
                certificateFileStore.Delete(certificateTicket);
            }
            catch (Exception ex)
            {
                certificateFileCleanupFailures++;
                logger.LogError(
                    ex,
                    "Certificate file {CertificateTicket} could not be removed after account deletion operation {OperationId}; a later orphan-file sweep may retry it.",
                    certificateTicket,
                    operationId);
            }
        }

        logger.LogInformation(
            "Account deletion operation {OperationId} completed for reason {Reason}; account found: {AccountFound}. Removed {AiAuditCount} AI audit records and {CertificationCount} certification requests. Attempted cleanup for {CertificateFileAttemptCount} certificate tickets; {CertificateFileFailureCount} attempts failed.",
            operationId,
            reason,
            accountFound,
            deletedAiAudits,
            deletedCertificationRequests,
            certificateFileCleanupAttempts,
            certificateFileCleanupFailures);

        return new AccountDeletionResult(
            accountFound,
            true,
            deletedAiAudits,
            deletedCertificationRequests,
            certificateFileCleanupAttempts,
            certificateFileCleanupFailures,
            []);
    }
}
