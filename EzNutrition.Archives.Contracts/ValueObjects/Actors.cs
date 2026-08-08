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
    /// 获取无法确认主体时的明确原因。
    /// </summary>
    public DataAbsentReasonCode? AbsentReason { get; init; }
}
