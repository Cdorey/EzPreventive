using EzNutrition.Archives.Contracts.Serialization;

namespace EzNutrition.Archives.Xml;

/// <summary>
/// 提供 EzNutrition XML 档案格式的稳定标识。
/// </summary>
public static class XmlArchiveFormat
{
    /// <summary>获取 XML codec 的稳定标识。</summary>
    public static Uri CodecIdentifier { get; } =
        new("https://eznutrition.cdorey.net/codecs/archive-xml");

    /// <summary>获取 XML 档案格式标识。</summary>
    public static Uri FormatIdentifier { get; } =
        new("https://eznutrition.cdorey.net/formats/archive-xml");

    /// <summary>获取当前 XML 格式版本。</summary>
    public const string CurrentVersion = "1.0";

    /// <summary>获取 XML 文档命名空间。</summary>
    public const string Namespace = "https://eznutrition.cdorey.net/formats/archive-xml/1";

    /// <summary>获取 XML 档案媒体类型。</summary>
    public const string MediaType = "application/vnd.eznutrition.archive+xml";

    /// <summary>获取当前格式描述。</summary>
    public static ArchiveFormatDescriptor Current { get; } = new(
        FormatIdentifier,
        CurrentVersion,
        MediaType,
        "EzNutrition XML 档案",
        ".xml");
}

/// <summary>
/// 提供 XML codec 返回的稳定校验代码。
/// </summary>
public static class XmlArchiveValidationCodes
{
    /// <summary>XML 文档结构非法或无法解析。</summary>
    public const string InvalidDocument = "archive.xml.document-invalid";

    /// <summary>XML 文档格式版本不受支持。</summary>
    public const string UnsupportedVersion = "archive.xml.version-unsupported";

    /// <summary>XML 文档包含不受支持的资源类型。</summary>
    public const string UnsupportedResource = "archive.xml.resource-unsupported";

    /// <summary>未知 XML 内容无法在语义变更后安全回写。</summary>
    public const string UnknownContentConflict = "archive.xml.unknown-content-conflict";

    /// <summary>请求的目标格式不是本 codec 支持的格式。</summary>
    public const string UnsupportedTargetFormat = "archive.xml.target-format-unsupported";
}
