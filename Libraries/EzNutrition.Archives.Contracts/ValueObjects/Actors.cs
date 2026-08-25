using EzNutrition.Archives.Contracts.Identity;

namespace EzNutrition.Archives.Contracts.ValueObjects;

/// <summary>
/// 表示产生档案数据的应用及其版本。
/// </summary>
public sealed record ApplicationIdentity
{
    /// <summary>
    /// 初始化应用身份。
    /// </summary>
    /// <param name="identifier">应用的稳定绝对 URI。</param>
    /// <param name="name">应用显示名称。</param>
    /// <param name="version">应用版本。</param>
    public ApplicationIdentity(Uri identifier, string name, string version)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        if (!identifier.IsAbsoluteUri)
        {
            throw new ArgumentException("应用标识必须使用绝对 URI。", nameof(identifier));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        Identifier = identifier;
        Name = name.Trim();
        Version = version.Trim();
    }

    /// <summary>
    /// 获取稳定应用标识。
    /// </summary>
    public Uri Identifier { get; }

    /// <summary>
    /// 获取应用名称。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 获取应用版本。
    /// </summary>
    public string Version { get; }
}

/// <summary>
/// 表示档案中的人员、机构或其他外部主体引用。
/// </summary>
public sealed record ActorReference
{
    /// <summary>
    /// 获取主体种类编码，例如医师、机构或设备；来源未区分主体种类时为空。
    /// </summary>
    public Coding? Kind { get; init; }

    /// <summary>
    /// 获取可选的档案内逻辑资源引用。
    /// </summary>
    public LogicalResourceReference? ResourceReference { get; init; }

    /// <summary>
    /// 获取可选的外部业务标识。
    /// </summary>
    public BusinessIdentifier? Identifier { get; init; }

    /// <summary>
    /// 获取可选显示文本。
    /// </summary>
    public string? Display { get; init; }

    /// <summary>
    /// 获取当前主体在本次档案行为中代表或所属的可选机构。
    /// </summary>
    /// <remarks>
    /// 该字段保存行为发生时的单层机构快照，不表示当前机构成员关系，也不承载授权或权限声明。
    /// 机构自身不得继续设置所属机构；组织层级应由独立的机构资料表达。
    /// </remarks>
    public ActorReference? Organization { get; init; }

    /// <summary>
    /// 获取无法确认主体时的明确原因。
    /// </summary>
    public DataAbsentReasonCode? AbsentReason { get; init; }
}
