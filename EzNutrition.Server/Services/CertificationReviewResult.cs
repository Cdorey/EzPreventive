namespace EzNutrition.Server.Services;

/// <summary>表示认证申请审核信息的更新结果，不包含 HTTP 状态码。</summary>
public enum CertificationReviewStatus
{
    /// <summary>审核信息已保存。</summary>
    Updated,

    /// <summary>申请不存在。</summary>
    NotFound,

    /// <summary>目标审核状态未定义。</summary>
    InvalidStatus,

    /// <summary>没有携带有效的并发版本。</summary>
    InvalidVersion,

    /// <summary>待审核状态或原版本不符，或人工决定重试后仍存在并发冲突。</summary>
    Conflict,

    /// <summary>申请的提交时间不满足本轮超时条件。</summary>
    NotExpired
}

/// <summary>返回审核更新结果；图片清理失败仍视为审核信息已保存。</summary>
/// <param name="Status">审核信息的更新结果。</param>
/// <param name="CertificateFileCleanupFailed">是否有证件图片需要后续孤儿文件清理补偿。</param>
/// <param name="Version">保存成功后的新版本，其他结果为空。</param>
public sealed record CertificationReviewResult(
    CertificationReviewStatus Status,
    bool CertificateFileCleanupFailed = false,
    Guid? Version = null);
