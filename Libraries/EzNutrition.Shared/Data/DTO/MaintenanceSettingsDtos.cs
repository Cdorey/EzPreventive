namespace EzNutrition.Shared.Data.DTO;

/// <summary>表示站点维护功能使用的全部运行时配置。</summary>
/// <param name="CleanupSchedule">清理任务公共调度配置及其持久化元数据。</param>
/// <param name="AccountCleanup">账号清理配置及其持久化元数据。</param>
/// <param name="CertificationRequestCleanup">认证申请清理配置及其持久化元数据。</param>
/// <param name="LlmAuditCleanup">LLM 审计记录清理配置及其持久化元数据。</param>
public sealed record MaintenanceSettingsDto(
    DatabaseSettingDto<CleanupScheduleSettingsDto> CleanupSchedule,
    DatabaseSettingDto<AccountCleanupSettingsDto> AccountCleanup,
    DatabaseSettingDto<CertificationRequestCleanupSettingsDto> CertificationRequestCleanup,
    DatabaseSettingDto<LlmAuditCleanupSettingsDto> LlmAuditCleanup);

/// <summary>所有定期清理任务共用的调度配置。</summary>
public sealed class CleanupScheduleSettingsDto
{
    /// <summary>获取或设置每日启动一轮清理的服务器本地时间。</summary>
    public TimeOnly StartTime { get; set; } = new(3, 30);
}

/// <summary>表示一组可独立编辑的数据库配置及其版本信息。</summary>
/// <typeparam name="T">配置值类型。</typeparam>
/// <param name="Value">当前配置值的副本。</param>
/// <param name="Version">保存时必须原样提交的并发版本；空值表示数据库中尚无记录。</param>
/// <param name="SchemaVersion">配置文档的结构版本。</param>
/// <param name="UpdatedAtUtc">最近一次持久化修改时间。</param>
/// <param name="UpdatedByUserId">最近一次修改者的用户标识。</param>
public sealed record DatabaseSettingDto<T>(
    T Value,
    Guid? Version,
    int SchemaVersion,
    DateTime? UpdatedAtUtc,
    string? UpdatedByUserId) where T : class;

/// <summary>提交一组完整配置，并以读取时的版本防止覆盖其他管理员的修改。</summary>
/// <typeparam name="T">配置值类型。</typeparam>
/// <param name="Value">需要保存的完整配置值。</param>
/// <param name="ExpectedVersion">读取配置时获得的版本；首次创建时为空。</param>
public sealed record DatabaseSettingUpdateDto<T>(T Value, Guid? ExpectedVersion) where T : class;

/// <summary>账号清理的运行时配置。</summary>
public sealed class AccountCleanupSettingsDto
{
    /// <summary>获取或设置是否清理没有合法角色且从未提交认证申请的账号。</summary>
    public bool UnsubmittedCertificationCleanupEnabled { get; set; }

    /// <summary>获取或设置从注册开始计算的认证申请宽限天数。</summary>
    public int? CertificationSubmissionGraceDays { get; set; }

    /// <summary>获取或设置是否清理没有合法角色的账号。</summary>
    public bool NonFormalAccountCleanupEnabled { get; set; }

    /// <summary>获取或设置从账号创建时间起算的非正式账号保留天数。</summary>
    public int? NonFormalAccountRetentionDays { get; set; }

    /// <summary>获取或设置是否清理长期未登录的账号；该规则不检查角色或认证申请。</summary>
    public bool InactiveFormalAccountCleanupEnabled { get; set; }

    /// <summary>获取或设置账号允许连续未登录的天数。</summary>
    public int? FormalAccountInactivityDays { get; set; }
}

/// <summary>认证申请超时处理的运行时配置。</summary>
public sealed class CertificationRequestCleanupSettingsDto
{
    /// <summary>获取或设置是否自动拒绝超时待审核申请。</summary>
    public bool AutoRejectEnabled { get; set; }

    /// <summary>获取或设置从申请提交开始计算的待审核超时天数。</summary>
    public int? PendingTimeoutDays { get; set; }
}

/// <summary>LLM 调用审计记录清理的运行时配置。</summary>
public sealed class LlmAuditCleanupSettingsDto
{
    /// <summary>获取或设置是否清理超过保留期限的审计记录。</summary>
    public bool Enabled { get; set; }

    /// <summary>获取或设置审计记录的保留天数。</summary>
    public int? RetentionDays { get; set; }
}

/// <summary>表示数据库配置保存时发生的并发冲突。</summary>
/// <param name="Key">发生冲突的配置组标识。</param>
/// <param name="Message">供管理员理解和处理冲突的说明。</param>
public sealed record DatabaseSettingConflictDto(string Key, string Message);
