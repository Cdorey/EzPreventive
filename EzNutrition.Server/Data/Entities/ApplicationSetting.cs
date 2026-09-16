namespace EzNutrition.Server.Data.Entities;

/// <summary>保存一组可在运行时修改的应用配置。</summary>
public sealed class ApplicationSetting
{
    /// <summary>获取或设置稳定的配置组标识，不随 Options 类名变化。</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>获取或设置该组配置的完整 JSON 文档。</summary>
    public string ValueJson { get; set; } = "{}";

    /// <summary>获取或设置文档结构版本，用于阻止不兼容版本覆盖配置。</summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>获取或设置并发版本；每次保存时由应用重新生成。</summary>
    public Guid Version { get; set; }

    /// <summary>获取或设置最近一次修改的 UTC 时间。</summary>
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>获取或设置修改者标识；系统修改时为空，不关联账号的删除生命周期。</summary>
    public string? UpdatedByUserId { get; set; }
}
