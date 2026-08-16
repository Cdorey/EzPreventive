namespace EzNutrition.Archives.Contracts.Serialization;

/// <summary>
/// 表示一种档案编码格式及其精确版本。
/// </summary>
/// <remarks>
/// 该类型不假设格式为 XML；JSON、二进制或其他适配器可以使用同一描述方式。
/// </remarks>
public sealed record ArchiveFormatDescriptor
{
    /// <summary>
    /// 初始化档案格式描述。
    /// </summary>
    /// <param name="identifier">格式或规范的稳定绝对 URI。</param>
    /// <param name="version">精确格式版本。</param>
    /// <param name="mediaType">可选媒体类型。</param>
    /// <param name="displayName">可选的人类可读格式名称。</param>
    /// <param name="preferredFileExtension">可选的首选文件扩展名，必须以句点开头。</param>
    public ArchiveFormatDescriptor(
        Uri identifier,
        string version,
        string? mediaType = null,
        string? displayName = null,
        string? preferredFileExtension = null)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        if (!identifier.IsAbsoluteUri)
        {
            throw new ArgumentException("档案格式标识必须使用绝对 URI。", nameof(identifier));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        Identifier = identifier;
        Version = version.Trim();
        MediaType = string.IsNullOrWhiteSpace(mediaType) ? null : mediaType.Trim();
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
        PreferredFileExtension = NormalizeFileExtension(preferredFileExtension);
    }

    /// <summary>
    /// 获取格式或规范的稳定标识。
    /// </summary>
    public Uri Identifier { get; }

    /// <summary>
    /// 获取精确格式版本。
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// 获取可选媒体类型。
    /// </summary>
    public string? MediaType { get; }

    /// <summary>
    /// 获取由格式实现声明的人类可读名称；未声明时为空。
    /// </summary>
    public string? DisplayName { get; }

    /// <summary>
    /// 获取由格式实现声明的首选文件扩展名；格式不面向文件时可以为空。
    /// </summary>
    public string? PreferredFileExtension { get; }

    private static string? NormalizeFileExtension(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var extension = value.Trim();
        if (extension.Length < 2 ||
            extension[0] != '.' ||
            extension.IndexOfAny(['/', '\\']) >= 0)
        {
            throw new ArgumentException("首选文件扩展名必须以句点开头，且不能包含路径分隔符。", nameof(value));
        }

        return extension;
    }
}
