namespace EzNutrition.Archives.Contracts.ValueObjects;

/// <summary>
/// 表示由医院、社区或其他机构签发的业务标识。
/// </summary>
/// <remarks>
/// 业务标识不得替代档案资源的逻辑 UUID，也不应直接用作文件名。
/// </remarks>
public sealed record BusinessIdentifier
{
    /// <summary>
    /// 初始化业务标识。
    /// </summary>
    /// <param name="system">签发体系的绝对 URI。</param>
    /// <param name="value">该体系内的标识值。</param>
    /// <param name="type">可选的标识类型编码。</param>
    /// <param name="assignerDisplay">可选的签发机构显示名。</param>
    public BusinessIdentifier(Uri system, string value, Coding? type = null, string? assignerDisplay = null)
    {
        ArgumentNullException.ThrowIfNull(system);
        if (!system.IsAbsoluteUri)
        {
            throw new ArgumentException("业务标识体系必须使用绝对 URI。", nameof(system));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        System = system;
        Value = value.Trim();
        Type = type;
        AssignerDisplay = string.IsNullOrWhiteSpace(assignerDisplay) ? null : assignerDisplay.Trim();
    }

    /// <summary>
    /// 获取签发体系 URI。
    /// </summary>
    public Uri System { get; }

    /// <summary>
    /// 获取业务标识值。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// 获取可选的标识类型。
    /// </summary>
    public Coding? Type { get; }

    /// <summary>
    /// 获取可选的签发机构显示名。
    /// </summary>
    public string? AssignerDisplay { get; }
}
