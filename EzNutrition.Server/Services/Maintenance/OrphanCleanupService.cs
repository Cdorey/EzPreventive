using EzNutrition.Server.Data;
using EzNutrition.Server.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EzNutrition.Server.Services.Maintenance;

/// <summary>
/// 表示一次孤儿证件文件清理的统计结果。
/// </summary>
/// <param name="RecognizedFiles">目录中符合本程序文件命名规则的文件数量。</param>
/// <param name="CleanupCandidates">早于时间界限且被判定为可清理对象的文件数量。</param>
/// <param name="DeletedFiles">本次实际删除的文件数量。</param>
/// <param name="FailedFiles">因输入输出或访问权限错误而删除失败的文件数量。</param>
public sealed record OrphanFileCleanupResult(
    int RecognizedFiles,
    int CleanupCandidates,
    int DeletedFiles,
    int FailedFiles);

/// <summary>
/// 提供不依赖具体调度方式的孤儿文件和孤儿 AI 审计记录清理操作。
/// </summary>
/// <remarks>
/// 本服务不决定执行周期。后台服务、外部计划任务、管理命令或测试均可直接调用其公开方法。
/// </remarks>
/// <param name="applicationDb">认证申请、AI 审计记录及 Identity 用户所在的数据库上下文。</param>
/// <param name="certificateFileStore">证件文件存储。</param>
/// <param name="logger">日志记录器。</param>
public sealed class OrphanCleanupService(
    ApplicationDbContext applicationDb,
    CertificateFileStore certificateFileStore,
    ILogger<OrphanCleanupService> logger)
{
    /// <summary>
    /// 删除临时上传目录中早于指定时间且已失去数据库引用的证件文件，以及过期的上传临时文件。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 最终证件文件仅在其 Ticket 既不存在于数据库认证申请，也不存在于当前 EF Core
    /// 更改跟踪器时才会删除。符合本程序命名规则的上传临时文件只按时间界限判断。
    /// </para>
    /// <para>
    /// 无法识别的文件名不会被删除。单个文件删除失败会计入结果并继续处理其他文件，
    /// 因而后续再次调用本方法即可自然重试。
    /// </para>
    /// </remarks>
    /// <param name="lastModifiedBefore">仅处理最后修改时间严格早于此时间的文件。</param>
    /// <param name="cancellationToken">用于取消目录枚举和数据库查询的令牌。</param>
    /// <returns>本次扫描、候选、成功删除和删除失败的文件数量。</returns>
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

    /// <summary>
    /// 删除用户标识已无法关联到任何 Identity 用户的 AI 审计记录。
    /// </summary>
    /// <remarks>
    /// 已持久化的孤儿记录通过数据库端删除语句一次清理。尚未写入数据库且同样失去用户关联的
    /// EF Core 跟踪实体会被卸载，以防后续保存时重新写入；这些实体不计入返回数量。
    /// </remarks>
    /// <param name="cancellationToken">用于取消数据库查询和删除操作的令牌。</param>
    /// <returns>从数据库实际删除的 AI 审计记录数量。</returns>
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
