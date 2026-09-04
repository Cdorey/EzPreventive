namespace EzNutrition.Server.Services.Settings;

/// <summary>保存按请求提交时间清理 LLM 调用审计记录的运行时参数。</summary>
public sealed class LlmAuditCleanupOptions
{
    /// <summary>数据库中的稳定配置组标识。</summary>
    public const string SectionName = "LlmAuditCleanup";

    /// <summary>获取或设置是否清理超过保留期限的 LLM 审计记录。</summary>
    public bool Enabled { get; set; }

    /// <summary>获取或设置从 RequestTime 起算的审计记录保留天数；为空表示尚未配置。</summary>
    public int? RetentionDays { get; set; }

    /// <summary>获取或设置自动扫描的间隔小时数；为空表示尚未配置调度。</summary>
    public int? SweepIntervalHours { get; set; }
}
