namespace EzNutrition.Archives.Contracts.Serialization;

/// <summary>
/// 表示一种档案编码格式及其精确版本。
/// </summary>
/// <remarks>
/// 该类型不假设格式为 XML；FHIR、JSON 或其他适配器可以使用同一描述方式。
/// </remarks>
public sealed record ArchiveFormatDescriptor
{
    /// <summary>
    /// 初始化档案格式描述。
    /// </summary>
    /// <param name="identifier">格式或规范的稳定绝对 URI。</param>
    /// <param name="version">精确格式版本。</param>
    /// <param name="mediaType">可选媒体类型。</param>
    public ArchiveFormatDescriptor(Uri identifier, string version, string? mediaType = null)
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
}
