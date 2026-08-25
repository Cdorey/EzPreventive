namespace EzNutrition.Archives.Contracts.ValueObjects;

/// <summary>
/// 表示由绝对 URI 和可选版本构成的规范引用。
/// </summary>
public sealed record CanonicalReference
{
    /// <summary>
    /// 初始化规范引用。
    /// </summary>
    /// <param name="uri">绝对 URI。</param>
    /// <param name="version">可选版本。</param>
    public CanonicalReference(Uri uri, string? version = null)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri)
        {
            throw new ArgumentException("规范引用必须使用绝对 URI。", nameof(uri));
        }

        Uri = uri;
        Version = string.IsNullOrWhiteSpace(version) ? null : version.Trim();
    }

    /// <summary>
    /// 获取规范 URI。
    /// </summary>
    public Uri Uri { get; }

    /// <summary>
    /// 获取可选版本。
    /// </summary>
    public string? Version { get; }
}
