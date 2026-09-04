namespace EzNutrition.Shared.Data.DTO;

/// <summary>指定管理员要预览或执行的账号清理规则。</summary>
public enum AccountCleanupRule
{
    /// <summary>清理没有合法角色且从未提交认证申请的账号。</summary>
    CertificationNotRequested,

    /// <summary>清理没有合法角色的账号，不区分是否提交过认证申请。</summary>
    AccountWithoutRoles,

    /// <summary>清理长期未登录的账号，不额外检查角色或认证申请。</summary>
    InactiveAccount
}

/// <summary>请求预览一条账号清理规则。</summary>
/// <param name="Rule">需要预览的清理规则。</param>
public sealed record AccountCleanupPreviewRequestDto(AccountCleanupRule Rule);

/// <summary>请求执行一条已经读取过配置的账号清理规则。</summary>
/// <param name="Rule">需要执行的清理规则。</param>
/// <param name="ExpectedSettingsVersion">预览时获得的账号清理配置版本。</param>
/// <param name="CutoffUtc">预览返回的 UTC 截止时间。</param>
public sealed record AccountCleanupExecutionRequestDto(
    AccountCleanupRule Rule,
    Guid ExpectedSettingsVersion,
    DateTimeOffset CutoffUtc);

/// <summary>表示单个账号在一轮清理中的处理状态。</summary>
public enum AccountCleanupItemStatus
{
    /// <summary>当前符合清理条件，尚未执行删除。</summary>
    WouldDelete,

    /// <summary>账号及其数据库关联数据已删除。</summary>
    Deleted,

    /// <summary>执行时账号已不存在或不再符合条件。</summary>
    Skipped,

    /// <summary>删除未能确认成功。</summary>
    Failed
}

/// <summary>表示账号清理清单中的一项。</summary>
/// <param name="UserId">账号的稳定主键。</param>
/// <param name="UserName">初次筛选时的用户名。</param>
/// <param name="Status">当前处理状态。</param>
/// <param name="Reason">入选、跳过、失败或文件清理警告的说明。</param>
/// <param name="CertificateFileCleanupFailures">未能删除的证件文件 Ticket 数量。</param>
public sealed record AccountCleanupItemDto(
    string UserId,
    string? UserName,
    AccountCleanupItemStatus Status,
    string Reason,
    int CertificateFileCleanupFailures);

/// <summary>返回账号清理预览或执行结果。</summary>
/// <param name="Rule">本轮使用的清理规则。</param>
/// <param name="SettingsVersion">计算截止时间时读取的账号清理配置版本。</param>
/// <param name="CutoffUtc">本轮固定使用的 UTC 截止时间。</param>
/// <param name="DryRun">是否仅生成预览。</param>
/// <param name="Items">候选账号及其处理结果。</param>
/// <param name="IsCanceled">执行是否因请求取消而提前停止。</param>
public sealed record AccountCleanupOperationDto(
    AccountCleanupRule Rule,
    Guid SettingsVersion,
    DateTimeOffset CutoffUtc,
    bool DryRun,
    IReadOnlyList<AccountCleanupItemDto> Items,
    bool IsCanceled);
