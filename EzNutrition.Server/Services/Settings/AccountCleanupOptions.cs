namespace EzNutrition.Server.Services.Settings;

/// <summary>保存账号清理的可配置参数；候选账号判定和实际删除由清理服务负责。</summary>
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

    /// <summary>获取或设置从账号创建时间起算的非正式账号保留天数；为空表示尚未配置。</summary>
    public int? NonFormalAccountRetentionDays { get; set; }

    /// <summary>获取或设置是否清理长期未登录的账号；该规则不检查角色或认证申请。</summary>
    public bool InactiveFormalAccountCleanupEnabled { get; set; }

    /// <summary>获取或设置账号允许连续未登录的天数；为空表示尚未配置。</summary>
    public int? FormalAccountInactivityDays { get; set; }
}
