namespace EzNutrition.Server.Services.Settings;

/// <summary>返回可独立编辑的配置副本及其持久化版本；版本为空表示尚无数据库记录。</summary>
/// <param name="Value">配置副本，修改它不会改变正在使用的配置。</param>
/// <param name="Version">下次保存必须携带的并发版本。</param>
/// <param name="SchemaVersion">配置文档的结构版本。</param>
/// <param name="UpdatedAtUtc">最近一次持久化修改时间。</param>
/// <param name="UpdatedByUserId">最近一次修改者标识。</param>
public sealed record DatabaseSettingsValue<T>(
    T Value,
    Guid? Version,
    int SchemaVersion,
    DateTime? UpdatedAtUtc,
    string? UpdatedByUserId) where T : class;

/// <summary>表示配置已被其他写入者修改，调用方应重新读取而非覆盖重试。</summary>
public sealed class DatabaseSettingsConcurrencyException(string key, Exception? innerException = null)
    : Exception($"配置组 {key} 已发生变化，请重新读取后再保存。", innerException)
{
    /// <summary>获取发生冲突的配置组标识。</summary>
    public string Key { get; } = key;
}
