namespace EzNutrition.Server.Services.Settings;

/// <summary>保存所有定期清理任务共用的调度参数。</summary>
public sealed class CleanupScheduleOptions
{
    /// <summary>数据库中的稳定配置组标识。</summary>
    public const string SectionName = "CleanupSchedule";

    /// <summary>获取或设置每日启动一轮清理的服务器本地时间。</summary>
    public TimeOnly StartTime { get; set; } = new(3, 30);
}
