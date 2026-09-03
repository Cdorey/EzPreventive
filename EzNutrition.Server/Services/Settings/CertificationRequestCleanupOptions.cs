namespace EzNutrition.Server.Services.Settings;

/// <summary>保存认证申请超时处理参数；自动拒绝的候选判定和执行由后续清理策略负责。</summary>
public sealed class CertificationRequestCleanupOptions
{
    /// <summary>数据库中的稳定配置组标识。</summary>
    public const string SectionName = "CertificationRequestCleanup";

    /// <summary>获取或设置是否自动拒绝长时间未获管理员处理的待审核申请。</summary>
    public bool AutoRejectEnabled { get; set; }

    /// <summary>获取或设置申请等待管理员处理的超时天数；计时起点由策略统一，为空表示尚未配置。</summary>
    public int? PendingTimeoutDays { get; set; }

    /// <summary>获取或设置自动扫描的间隔小时数；为空表示尚未配置调度。</summary>
    public int? SweepIntervalHours { get; set; }
}
