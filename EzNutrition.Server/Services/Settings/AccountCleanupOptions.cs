namespace EzNutrition.Server.Services.Settings;

/// <summary>保存账号清理的可配置参数；候选账号判定和实际删除由后续清理策略负责。</summary>
public sealed class AccountCleanupOptions
{
    /// <summary>数据库中的稳定配置组标识。</summary>
    public const string SectionName = "AccountCleanup";

    /// <summary>获取或设置是否清理没有合法角色、且从未提交认证申请的非正式账号。</summary>
    public bool UnsubmittedCertificationCleanupEnabled { get; set; }

    /// <summary>获取或设置从注册开始计算的申请宽限天数；为空表示尚未配置。</summary>
    public int? CertificationSubmissionGraceDays { get; set; }

    /// <summary>获取或设置是否清理没有合法角色的非正式账号。</summary>
    public bool NonFormalAccountCleanupEnabled { get; set; }

    /// <summary>获取或设置非正式账号的保留天数；计时起点由清理策略统一，为空表示尚未配置。</summary>
    public int? NonFormalAccountRetentionDays { get; set; }

    /// <summary>获取或设置是否清理拥有合法角色、但长期未登录的正式账号。</summary>
    public bool InactiveFormalAccountCleanupEnabled { get; set; }

    /// <summary>获取或设置正式账号允许连续未登录的天数；为空表示尚未配置。</summary>
    public int? FormalAccountInactivityDays { get; set; }

    /// <summary>获取或设置自动扫描的间隔小时数；为空表示尚未配置调度。</summary>
    public int? SweepIntervalHours { get; set; }
}
