namespace EzNutrition.Server.Services.Maintenance;

/// <summary>表示单个候选账号的清理状态。</summary>
public enum AccountCleanupStatus
{
    /// <summary>初次筛选符合条件，尚未执行；用于预览或取消后剩余的条目。</summary>
    WouldDelete,

    /// <summary>账号已从数据库删除；证件文件可能仍需后续补偿清理。</summary>
    Deleted,

    /// <summary>执行时账号已不存在，或不再满足筛选条件。</summary>
    Skipped,

    /// <summary>删除操作未能确认成功，详情记录在原因和服务端日志中。</summary>
    Failed
}

/// <summary>返回账号清理明细，不暴露或跟踪 Identity 实体。</summary>
/// <param name="UserId">账号的稳定主键。</param>
/// <param name="UserName">初次筛选时的用户名。</param>
/// <param name="Status">当前处理状态。</param>
/// <param name="Reason">入选、跳过、失败或文件清理警告的说明。</param>
/// <param name="CertificateFileCleanupFailures">账号删除后未能清理的证件 Ticket 数量。</param>
public sealed record AccountCleanupItem(
    string UserId,
    string? UserName,
    AccountCleanupStatus Status,
    string Reason,
    int CertificateFileCleanupFailures = 0);

/// <summary>返回整轮清理结果；取消后保留已完成状态，未处理条目仍为 WouldDelete。</summary>
/// <param name="CutoffUtc">本轮固定使用的 UTC 截止时间。</param>
/// <param name="DryRun">是否仅预览。</param>
/// <param name="Items">初次筛选的全部候选及其处理结果，不设置数量上限。</param>
/// <param name="IsCanceled">是否在执行阶段响应取消并提前停止。</param>
/// <param name="ConfigurationChanged">自动扫描是否因配置变化而停止。</param>
public sealed record AccountCleanupResult(
    DateTimeOffset CutoffUtc,
    bool DryRun,
    IReadOnlyList<AccountCleanupItem> Items,
    bool IsCanceled = false,
    bool ConfigurationChanged = false);
