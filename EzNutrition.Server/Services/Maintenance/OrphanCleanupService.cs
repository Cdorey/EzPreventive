using EzNutrition.Server.Data;
using EzNutrition.Server.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EzNutrition.Server.Services.Maintenance;

public sealed record OrphanFileCleanupResult(
    int RecognizedFiles,
    int CleanupCandidates,
    int DeletedFiles,
    int FailedFiles);

public sealed class OrphanCleanupService(
    ApplicationDbContext applicationDb,
    CertificateFileStore certificateFileStore,
    ILogger<OrphanCleanupService> logger)
{
    public async Task<OrphanFileCleanupResult> DeleteOrphanedCertificateFilesAsync(
        DateTimeOffset lastModifiedBefore,
        CancellationToken cancellationToken = default)
    {
        var storedFiles = certificateFileStore.EnumerateStoredFiles(cancellationToken);
        var oldFiles = storedFiles
            .Where(file => file.LastModifiedUtc < lastModifiedBefore)
            .ToArray();
        var finalFileTickets = oldFiles
            .Where(file => file.Kind == StoredCertificateFileKind.Final)
            .Select(file => file.Ticket)
            .Distinct()
            .ToArray();

        var referencedTickets = finalFileTickets.Length == 0
            ? []
            : (await applicationDb.ProfessionalCertificationRequests
                .AsNoTracking()
                .Where(request => request.CertificateTicket != null &&
                    finalFileTickets.Contains(request.CertificateTicket.Value))
                .Select(request => request.CertificateTicket!.Value)
                .Distinct()
                .ToArrayAsync(cancellationToken))
                .ToHashSet();
        foreach (var entry in applicationDb.ChangeTracker.Entries<ProfessionalCertificationRequest>())
        {
            if (entry.Entity.CertificateTicket is Guid ticket)
            {
                referencedTickets.Add(ticket);
            }
        }

        var cleanupCandidates = oldFiles
            .Where(file => file.Kind == StoredCertificateFileKind.Temporary ||
                !referencedTickets.Contains(file.Ticket))
            .ToArray();
        var deletedFiles = 0;
        var failedFiles = 0;
        foreach (var file in cleanupCandidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (certificateFileStore.Delete(file))
                {
                    deletedFiles++;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failedFiles++;
                logger.LogWarning(
                    ex,
                    "Failed to remove orphaned certificate file for ticket {CertificateTicket} of kind {FileKind}; a later cleanup run may retry it.",
                    file.Ticket,
                    file.Kind);
            }
        }

        var result = new OrphanFileCleanupResult(
            storedFiles.Count,
            cleanupCandidates.Length,
            deletedFiles,
            failedFiles);
        logger.LogInformation(
            "Orphaned certificate-file cleanup examined {RecognizedFileCount} recognized files, selected {CandidateCount}, deleted {DeletedCount}, and failed to delete {FailureCount}.",
            result.RecognizedFiles,
            result.CleanupCandidates,
            result.DeletedFiles,
            result.FailedFiles);
        return result;
    }

    public async Task<int> DeleteOrphanedAiAuditRecordsAsync(
        CancellationToken cancellationToken = default)
    {
        var trackedAudits = applicationDb.ChangeTracker
            .Entries<PrescriptionGenerateRequest>()
            .ToArray();
        if (trackedAudits.Length > 0)
        {
            var trackedUserIds = trackedAudits
                .Select(entry => entry.Entity.UserId)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var existingUserIds = (await applicationDb.Users
                .AsNoTracking()
                .Where(user => trackedUserIds.Contains(user.Id))
                .Select(user => user.Id)
                .ToArrayAsync(cancellationToken))
                .ToHashSet(StringComparer.Ordinal);
            existingUserIds.UnionWith(applicationDb.ChangeTracker
                .Entries<IdentityUser>()
                .Where(entry => entry.State == EntityState.Added)
                .Select(entry => entry.Entity.Id));

            foreach (var entry in trackedAudits.Where(
                entry => !existingUserIds.Contains(entry.Entity.UserId)))
            {
                entry.State = EntityState.Detached;
            }
        }

        var deletedRecords = await applicationDb.PrescriptionGenerateRequests
            .Where(audit => !applicationDb.Users.Any(user => user.Id == audit.UserId))
            .ExecuteDeleteAsync(cancellationToken);
        logger.LogInformation(
            "Orphaned AI audit cleanup deleted {DeletedRecordCount} records.",
            deletedRecords);
        return deletedRecords;
    }
}
