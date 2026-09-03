using EzNutrition.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace EzNutrition.Server.Services.Maintenance;

/// <summary>提供可预览的账号清理入口，逐账号复核并复用统一删除流程。</summary>
/// <remarks>
/// 初次筛选仅加载账号主键和用户名，不限制候选数量。每个账号使用独立作用域和事务；
/// 执行阶段在账号之间响应取消，当前账号完成后才停止，确保返回已完成的处理结果。
/// 调用方负责授权、配置读取和调度，本服务不执行 HTTP 或后台任务。
/// </remarks>
public sealed class AccountCleanupService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<AccountCleanupService> logger)
{
    /// <summary>清理最近登录时间早于截止时间的账号，不检查角色或认证申请。</summary>
    /// <remarks>
    /// 从未登录的账号从创建时间起算；有效时间未知的历史账号不入选。
    /// 当前最近登录字段只记录密码登录，刷新会话不会更新该字段。
    /// </remarks>
    /// <param name="cutoffUtc">截止时间；严格早于此时间才入选，不允许使用未来时间。</param>
    /// <param name="dryRun">为 true 时只返回候选清单，不修改数据库和文件。</param>
    /// <param name="cancellationToken">取消信号；筛选阶段可取消查询，执行阶段在账号之间停止。</param>
    public Task<AccountCleanupResult> DeleteInactiveAccountsAsync(
        DateTimeOffset cutoffUtc,
        bool dryRun = true,
        CancellationToken cancellationToken = default)
    {
        var cutoff = cutoffUtc.UtcDateTime;
        return CleanupAsync(cutoffUtc, dryRun, AccountDeletionReason.InactiveAccountExpired,
            "最近登录时间早于截止时间；从未登录时使用创建时间。",
            db => db.Users.Where(user =>
                (user.LastSuccessfulLoginAtUtc ?? user.CreatedAtUtc) > DateTime.MinValue &&
                (user.LastSuccessfulLoginAtUtc ?? user.CreatedAtUtc) < cutoff),
            cancellationToken);
    }

    /// <summary>清理创建时间早于截止时间、且没有合法角色的账号，可进一步要求从未提交认证申请。</summary>
    /// <remarks>当前注册不分配默认角色；存在关联到 Identity 角色表的角色即视为有合法角色。</remarks>
    /// <param name="cutoffUtc">创建时间的截止界限；未知创建时间的账号不入选。</param>
    /// <param name="onlyWithoutApplications">为 true 时排除提交过任何状态认证申请的账号；为 false 时忽略申请记录。</param>
    /// <param name="dryRun">为 true 时只返回候选清单，不修改数据库和文件。</param>
    /// <param name="cancellationToken">取消信号；执行阶段在账号之间停止。</param>
    public Task<AccountCleanupResult> DeleteAccountsWithoutRolesAsync(
        DateTimeOffset cutoffUtc,
        bool onlyWithoutApplications,
        bool dryRun = true,
        CancellationToken cancellationToken = default)
    {
        var cutoff = cutoffUtc.UtcDateTime;
        return CleanupAsync(cutoffUtc, dryRun,
            onlyWithoutApplications ? AccountDeletionReason.CertificationNotRequestedExpired
                : AccountDeletionReason.AccountWithoutRolesExpired,
            onlyWithoutApplications ? "创建时间早于截止时间，没有合法角色且从未提交认证申请。"
                : "创建时间早于截止时间，且没有合法角色。",
            db => db.Users.Where(user =>
                user.CreatedAtUtc > DateTime.MinValue && user.CreatedAtUtc < cutoff &&
                !db.UserRoles.Any(link => link.UserId == user.Id && db.Roles.Any(role => role.Id == link.RoleId)) &&
                (!onlyWithoutApplications || !db.ProfessionalCertificationRequests.Any(request => request.UserId == user.Id))),
            cancellationToken);
    }

    /// <summary>冻结本轮候选清单，预览直接返回，执行则逐账号在事务中重新应用同一条件。</summary>
    private async Task<AccountCleanupResult> CleanupAsync(
        DateTimeOffset cutoffUtc,
        bool dryRun,
        AccountDeletionReason reason,
        string candidateReason,
        Func<ApplicationDbContext, IQueryable<ApplicationUser>> eligibleUsers,
        CancellationToken cancellationToken)
    {
        if (cutoffUtc <= DateTimeOffset.MinValue || cutoffUtc > timeProvider.GetUtcNow())
        {
            throw new ArgumentOutOfRangeException(nameof(cutoffUtc), "截止时间必须是有效的当前或过去时间。");
        }
        if (System.Transactions.Transaction.Current is not null)
        {
            throw new InvalidOperationException("账号清理必须使用独立事务，不能加入外部环境事务。");
        }

        List<AccountCleanupItem> items;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            items = await eligibleUsers(db).AsNoTracking().OrderBy(user => user.Id)
                .Select(user => new AccountCleanupItem(user.Id, user.UserName,
                    AccountCleanupStatus.WouldDelete, candidateReason, 0))
                .ToListAsync(cancellationToken);
        }

        if (!dryRun)
        {
            for (var index = 0; index < items.Count; index++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return new(cutoffUtc.ToUniversalTime(), false, items, IsCanceled: true);
                }

                var item = items[index];
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var deletion = scope.ServiceProvider.GetRequiredService<AccountDeletionService>();
                    // 单账号一旦开始就完成事务和结果记录，取消只影响后续账号。
                    var result = await deletion.DeleteIfEligibleAsync(item.UserId, reason, eligibleUsers);
                    items[index] = result switch
                    {
                        null => item with { Status = AccountCleanupStatus.Skipped, Reason = "账号已不存在或不再满足清理条件。" },
                        { Succeeded: false } => item with
                        {
                            Status = AccountCleanupStatus.Failed,
                            Reason = $"Identity 拒绝删除：{string.Join(",", result.IdentityErrors.Select(error => error.Code))}。"
                        },
                        _ => item with
                        {
                            Status = AccountCleanupStatus.Deleted,
                            Reason = result.CertificateFileCleanupFailures == 0 ? "账号已删除。"
                                : "账号已删除，部分证件文件清理失败，需由孤儿文件清理补偿。",
                            CertificateFileCleanupFailures = result.CertificateFileCleanupFailures
                        }
                    };
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "清理账号 {UserId} 失败，原因类型为 {Reason}。", item.UserId, reason);
                    items[index] = item with { Status = AccountCleanupStatus.Failed, Reason = "删除未能确认成功，详情见服务端日志。" };
                }
            }
        }

        return new(cutoffUtc.ToUniversalTime(), dryRun, items);
    }
}
