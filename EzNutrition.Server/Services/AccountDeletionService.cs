using EzNutrition.Server.Data;
using EzNutrition.Server.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace EzNutrition.Server.Services;

/// <summary>
/// 表示发起账号删除的业务原因。
/// </summary>
public enum AccountDeletionReason
{
    /// <summary>
    /// 管理员主动删除账号。
    /// </summary>
    AdministratorRequested,

    /// <summary>
    /// 用户主动申请删除自己的账号。
    /// </summary>
    UserRequested,

    /// <summary>
    /// 注册后的初始化步骤失败，需要回滚新建账号。
    /// </summary>
    RegistrationRollback,

    /// <summary>
    /// 账号在规定期限内未完成电子邮箱验证。
    /// </summary>
    UnconfirmedEmailExpired,

    /// <summary>
    /// 账号在规定期限内未完成专业身份认证。
    /// </summary>
    ProfessionalCertificationExpired,

    /// <summary>
    /// 账号超过规定期限未登录，不区分角色。
    /// </summary>
    InactiveAccountExpired,

    /// <summary>账号创建超过保留期限，且当前没有合法角色。</summary>
    AccountWithoutRolesExpired,

    /// <summary>账号创建超过申请宽限期限，没有合法角色且从未提交认证申请。</summary>
    CertificationNotRequestedExpired
}

/// <summary>
/// 表示一次账号及其关联数据删除操作的结果。
/// </summary>
/// <param name="AccountFound">执行删除时是否找到对应的 Identity 账号。</param>
/// <param name="Succeeded">数据库删除事务是否成功完成。</param>
/// <param name="DeletedAiAuditRecords">从数据库删除的 AI 审计记录数量。</param>
/// <param name="DeletedCertificationRequests">从数据库删除的专业认证申请数量。</param>
/// <param name="CertificateFileCleanupAttempts">事务提交后尝试清理的证件 Ticket 数量。</param>
/// <param name="CertificateFileCleanupFailures">证件文件清理失败的 Ticket 数量。</param>
/// <param name="IdentityErrors">Identity 删除失败时返回的错误；成功时为空。</param>
public sealed record AccountDeletionResult(
    bool AccountFound,
    bool Succeeded,
    int DeletedAiAuditRecords,
    int DeletedCertificationRequests,
    int CertificateFileCleanupAttempts,
    int CertificateFileCleanupFailures,
    IReadOnlyList<IdentityError> IdentityErrors);

/// <summary>
/// 统一删除 Identity 账号及其专业认证申请、AI 审计记录和证件文件。
/// </summary>
/// <remarks>
/// 数据库记录和 Identity 账号在同一事务内删除。证件文件在事务提交后尽力清理，
/// 文件清理失败不会回滚已经完成的数据库删除，可由后续孤儿文件清理再次处理。
/// </remarks>
/// <param name="applicationDb">账号及业务数据所在的数据库上下文。</param>
/// <param name="userManager">Identity 用户管理器。</param>
/// <param name="certificateFileStore">证件文件存储。</param>
/// <param name="logger">日志记录器。</param>
public sealed class AccountDeletionService(
    ApplicationDbContext applicationDb,
    UserManager<ApplicationUser> userManager,
    CertificateFileStore certificateFileStore,
    ILogger<AccountDeletionService> logger)
{
    /// <summary>
    /// 删除指定用户的账号及全部由本服务保存的用户关联数据。
    /// </summary>
    /// <remarks>
    /// 即使 Identity 账号已经不存在，本方法仍会幂等清理相同用户标识下残留的认证申请、
    /// AI 审计记录和可识别的证件文件。返回结果中的数据库删除数量不包含仅存在于
    /// EF Core 更改跟踪器、尚未写入数据库而被丢弃的实体。
    /// </remarks>
    /// <param name="userId">Identity 用户的稳定主键。</param>
    /// <param name="reason">本次删除的业务原因，用于结构化日志和后续审计。</param>
    /// <param name="cancellationToken">用于取消数据库操作的令牌。</param>
    /// <returns>账号是否存在、各类数据删除数量、文件清理结果及 Identity 错误。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="userId"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="ArgumentException"><paramref name="userId"/> 为空或仅包含空白字符。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="reason"/> 不是已定义的删除原因。</exception>
    public async Task<AccountDeletionResult> DeleteAsync(
        string userId,
        AccountDeletionReason reason,
        CancellationToken cancellationToken = default)
        => await DeleteCoreAsync(userId, reason, null, cancellationToken)
            ?? throw new InvalidOperationException("无条件账号删除不能返回跳过结果。");

    /// <summary>在可串行化事务中复核候选条件并删除；条件已不满足时返回空值。</summary>
    /// <remarks>调用方必须提供独立作用域，确保查询和 Identity 使用同一个干净的 DbContext。</remarks>
    internal Task<AccountDeletionResult?> DeleteIfEligibleAsync(
        string userId,
        AccountDeletionReason reason,
        Func<ApplicationDbContext, IQueryable<ApplicationUser>> eligibleUsers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eligibleUsers);
        return DeleteCoreAsync(userId, reason, eligibleUsers, cancellationToken);
    }

    /// <summary>共享单账号数据库删除和提交后的文件清理流程。</summary>
    private async Task<AccountDeletionResult?> DeleteCoreAsync(
        string userId,
        AccountDeletionReason reason,
        Func<ApplicationDbContext, IQueryable<ApplicationUser>>? eligibleUsers,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown account deletion reason.");
        }

        var operationId = Guid.NewGuid();
        var deletedAiAudits = 0;
        var deletedCertificationRequests = 0;
        bool accountFound;
        Guid[] certificateTickets;
        await using (var transaction = eligibleUsers is null
            ? await applicationDb.Database.BeginTransactionAsync(cancellationToken)
            : await applicationDb.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken))
        {
            if (eligibleUsers is not null && !await eligibleUsers(applicationDb)
                .AnyAsync(candidate => candidate.Id == userId, cancellationToken))
            {
                return null;
            }

            var user = await userManager.FindByIdAsync(userId);
            accountFound = user is not null;

            var trackedCertificationRequests = applicationDb.ChangeTracker
                .Entries<ProfessionalCertificationRequest>()
                .Where(entry => entry.Entity.UserId == userId)
                .ToArray();
            var trackedAiAudits = applicationDb.ChangeTracker
                .Entries<PrescriptionGenerateRequest>()
                .Where(entry => entry.Entity.UserId == userId)
                .ToArray();
            var persistedCertificateTickets = await applicationDb.ProfessionalCertificationRequests
                .AsNoTracking()
                .Where(request => request.UserId == userId && request.CertificateTicket != null)
                .Select(request => request.CertificateTicket!.Value)
                .ToArrayAsync(cancellationToken);
            certificateTickets = trackedCertificationRequests
                .Where(entry => entry.Entity.CertificateTicket is not null)
                .Select(entry => entry.Entity.CertificateTicket!.Value)
                .Concat(persistedCertificateTickets)
                .Distinct()
                .ToArray();

            foreach (var entry in trackedCertificationRequests)
            {
                entry.State = EntityState.Detached;
            }
            foreach (var entry in trackedAiAudits)
            {
                entry.State = EntityState.Detached;
            }

            // 会话轮换使用数据库条件更新，跟踪器中的旧并发版本不能参与级联删除。
            var trackedSessions = applicationDb.ChangeTracker.Entries<AuthenticationSession>()
                .Where(entry => entry.Entity.UserId == userId).ToArray();
            var sessionIds = await applicationDb.AuthenticationSessions
                .Where(session => session.UserId == userId)
                .Select(session => session.Id).ToHashSetAsync(cancellationToken);
            sessionIds.UnionWith(trackedSessions.Select(entry => entry.Entity.Id));
            foreach (var entry in applicationDb.ChangeTracker.Entries<RefreshTokenRecord>()
                .Where(entry => sessionIds.Contains(entry.Entity.SessionId)).ToArray())
            {
                entry.State = EntityState.Detached;
            }
            foreach (var entry in trackedSessions)
            {
                entry.State = EntityState.Detached;
            }

            await applicationDb.AuthenticationSessions.Where(session => session.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);
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
