namespace EzNutrition.Server.Services.Maintenance;

/// <summary>表示单条超时认证申请的处理状态。</summary>
public enum CertificationRequestCleanupStatus
{
    /// <summary>初筛符合条件，尚未执行；用于预览或中止后剩余的条目。</summary>
    WouldReject,
    /// <summary>已拒绝；证件图片可能仍需后续补偿清理。</summary>
    Rejected,
    /// <summary>申请已不存在、不再超时，或状态、版本发生变化；本轮不重试。</summary>
    Skipped,
    /// <summary>未能确认处理成功，详情见服务端日志。</summary>
    Failed
}

/// <summary>返回申请初筛快照及本轮执行结果，不包含证件图片和完整申请内容。</summary>
/// <param name="RequestId">申请主键。</param>
/// <param name="UserId">申请人的主键。</param>
/// <param name="RequestTime">初筛时的 UTC 提交时间。</param>
/// <param name="Version">初筛时的申请版本。</param>
/// <param name="Status">当前处理状态。</param>
/// <param name="Reason">入选、跳过或失败原因。</param>
/// <param name="CertificateFileCleanupFailed">是否需要后续补偿清理证件图片。</param>
public sealed record CertificationRequestCleanupItem(
    Guid RequestId, string UserId, DateTime RequestTime, Guid Version,
    CertificationRequestCleanupStatus Status, string Reason,
    bool CertificateFileCleanupFailed = false);

/// <summary>返回完整初筛清单和处理结果；停止后未处理项保持 WouldReject。</summary>
/// <param name="CutoffUtc">本轮固定的 UTC 截止时间。</param>
/// <param name="DryRun">是否仅预览。</param>
/// <param name="Items">全部初筛候选及处理结果，不设数量上限。</param>
/// <param name="IsCanceled">是否在执行阶段响应取消并停止。</param>
/// <param name="ConfigurationChanged">自动扫描是否因配置变化而停止。</param>
public sealed record CertificationRequestCleanupResult(
    DateTimeOffset CutoffUtc, bool DryRun, IReadOnlyList<CertificationRequestCleanupItem> Items,
    bool IsCanceled = false, bool ConfigurationChanged = false);
