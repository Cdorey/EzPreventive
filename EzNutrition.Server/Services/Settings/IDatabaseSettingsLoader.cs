namespace EzNutrition.Server.Services.Settings;

/// <summary>供启动初始化和后台重载枚举已注册的数据库配置组。</summary>
public interface IDatabaseSettingsLoader
{
    /// <summary>获取稳定的配置组标识。</summary>
    string Key { get; }

    /// <summary>从数据库加载并校验整组配置，成功后发布新的内存快照。</summary>
    Task ReloadAsync(CancellationToken cancellationToken = default);
}
