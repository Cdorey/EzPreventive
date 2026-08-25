namespace EzNutrition.Archives.Contracts.ValueObjects;

/// <summary>
/// 表示由某个代码体系定义的机器可识别代码。
/// </summary>
public sealed record Coding
{
    /// <summary>
    /// 初始化编码。
    /// </summary>
    /// <param name="system">控制该代码的绝对 URI 标识。</param>
    /// <param name="code">稳定机器代码。</param>
    /// <param name="version">可选的代码体系版本。</param>
    /// <param name="display">可选的人类可读显示文本，不参与身份匹配。</param>
    public Coding(Uri system, string code, string? version = null, string? display = null)
    {
        ArgumentNullException.ThrowIfNull(system);
        if (!system.IsAbsoluteUri)
        {
            throw new ArgumentException("代码体系必须使用绝对 URI 标识。", nameof(system));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        System = system;
        Code = code.Trim();
        Version = NormalizeOptional(version);
        Display = NormalizeOptional(display);
    }

    /// <summary>
    /// 获取代码体系 URI。
    /// </summary>
    public Uri System { get; }

    /// <summary>
    /// 获取机器代码。
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// 获取代码体系版本。
    /// </summary>
    public string? Version { get; }

    /// <summary>
    /// 获取显示文本。
    /// </summary>
    public string? Display { get; }

    /// <summary>
    /// 判断两个编码是否具有相同的机器身份。
    /// </summary>
    /// <param name="other">待比较编码。</param>
    /// <returns>体系、代码和版本相同时返回 <see langword="true"/>。</returns>
    public bool HasSameIdentity(Coding? other) =>
        other is not null &&
        System == other.System &&
        string.Equals(Code, other.Code, StringComparison.Ordinal) &&
        string.Equals(Version, other.Version, StringComparison.Ordinal);

    /// <summary>
    /// 判断两个编码是否具有相同的机器身份。
    /// </summary>
    /// <param name="other">待比较编码。</param>
    /// <returns>机器身份相同时返回 <see langword="true"/>。</returns>
    public bool Equals(Coding? other) => HasSameIdentity(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(System, Code, Version);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
