using EzNutrition.Server.Data.Entities;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EzNutrition.Server.Services.Settings;

/// <summary>以整组内存快照连接数据库配置与标准 Options，读取 Options 时不访问数据库。</summary>
/// <typeparam name="T">仅包含可序列化公共属性的配置类型。</typeparam>
public sealed class DatabaseOptions<T> : IConfigureOptions<T>, IOptionsChangeTokenSource<T>
    where T : class, new()
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.Strict,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private readonly IValidateOptions<T>[] validators;
    private readonly ILogger<DatabaseOptions<T>> logger;
    private State state = new(null, new ConfigurationReloadToken());

    /// <summary>为指定配置组建立独立的校验、快照和变更通知。</summary>
    public DatabaseOptions(string key, int schemaVersion, IEnumerable<IValidateOptions<T>> validators,
        ILogger<DatabaseOptions<T>> logger)
    {
        Key = key;
        SchemaVersion = schemaVersion;
        this.validators = validators.ToArray();
        this.logger = logger;
    }

    /// <summary>获取稳定的数据库配置组标识。</summary>
    public string Key { get; }

    /// <summary>获取当前程序支持的文档结构版本。</summary>
    public int SchemaVersion { get; }

    /// <inheritdoc />
    public string Name => Options.DefaultName;

    /// <summary>串行协调同一进程中该组配置的读取、保存和发布。</summary>
    internal SemaphoreSlim UpdateGate { get; } = new(1, 1);

    /// <inheritdoc />
    public IChangeToken GetChangeToken() => Volatile.Read(ref state).ChangeToken;

    /// <inheritdoc />
    public void Configure(T options)
    {
        var snapshot = Volatile.Read(ref state).Snapshot
            ?? throw new InvalidOperationException($"配置组 {Key} 尚未加载，请先完成数据库配置初始化。");
        snapshot.Configuration.Bind(options);
    }

    /// <summary>冻结调用方提交的配置，后续校验与写入使用同一份文档。</summary>
    internal string Serialize(T value) => JsonSerializer.Serialize(value, JsonOptions);

    /// <summary>验证文档版本、结构与业务参数，生成尚未发布的快照。</summary>
    internal DatabaseSettingsSnapshot Prepare(ApplicationSetting? setting)
    {
        if (setting is not null &&
            (setting.SchemaVersion != SchemaVersion || setting.Version == Guid.Empty))
        {
            throw new InvalidDataException($"配置组 {Key} 的文档版本不受支持或并发版本无效。");
        }

        string canonicalJson;
        try
        {
            var value = setting is null ? new T() :
                JsonSerializer.Deserialize<T>(setting.ValueJson, JsonOptions)
                    ?? throw new JsonException("配置文档必须是对象。");
            canonicalJson = Serialize(value);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"配置组 {Key} 的 JSON 文档无效。", exception);
        }

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(canonicalJson));
        var configuration = new ConfigurationBuilder().AddJsonStream(stream).Build();
        var candidate = new T();
        configuration.Bind(candidate);
        var errors = validators.Select(validator => validator.Validate(Options.DefaultName, candidate))
            .Where(result => result.Failed)
            .SelectMany(result => result.Failures ?? [])
            .ToArray();
        if (errors.Length > 0)
        {
            throw new OptionsValidationException(Options.DefaultName, typeof(T), errors);
        }

        return new(configuration, setting?.Version, SchemaVersion,
            setting?.UpdatedAtUtc, setting?.UpdatedByUserId);
    }

    /// <summary>在持有更新门闩时替换整组快照；通知留到释放门闩后执行。</summary>
    internal ConfigurationReloadToken? Publish(DatabaseSettingsSnapshot snapshot)
    {
        var previous = Volatile.Read(ref state);
        if (previous.Snapshot is not null && previous.Snapshot.Version == snapshot.Version)
        {
            return null;
        }

        Volatile.Write(ref state, new State(snapshot, new ConfigurationReloadToken()));
        return previous.ChangeToken;
    }

    /// <summary>通知标准 Options 更新；订阅者异常不会将已提交的保存误报为失败。</summary>
    internal void Notify(ConfigurationReloadToken? changeToken)
    {
        try
        {
            changeToken?.OnReload();
        }
        catch (AggregateException exception)
        {
            logger.LogError(exception, "配置组 {Key} 已更新，但部分变更订阅者执行失败。", Key);
        }
    }

    /// <summary>生成与缓存隔离的编辑副本。</summary>
    internal static DatabaseSettingsValue<T> ToValue(DatabaseSettingsSnapshot snapshot)
    {
        var value = new T();
        snapshot.Configuration.Bind(value);
        return new(value, snapshot.Version, snapshot.SchemaVersion,
            snapshot.UpdatedAtUtc, snapshot.UpdatedByUserId);
    }

    private sealed record State(DatabaseSettingsSnapshot? Snapshot, ConfigurationReloadToken ChangeToken);
}

/// <summary>保存不可变的配置文档视图及元数据，不持有 EF 实体或 DbContext。</summary>
internal sealed record DatabaseSettingsSnapshot(
    IConfiguration Configuration,
    Guid? Version,
    int SchemaVersion,
    DateTime? UpdatedAtUtc,
    string? UpdatedByUserId);
