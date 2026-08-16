using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;
using EzNutrition.Archives.Contracts.Abstractions;
using EzNutrition.Archives.Contracts.Bundles;
using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Archives.Contracts.Serialization;
using EzNutrition.Archives.Contracts.Validation;
using EzNutrition.Archives.Contracts.ValueObjects;
using EzNutrition.Archives.Xml.Internal;

namespace EzNutrition.Archives.Xml;

/// <summary>
/// 在类型化档案契约与 EzNutrition 版本化 XML 文档之间进行安全转换。
/// </summary>
public sealed class XmlArchiveCodec : IArchiveCodec
{
    private const long MaximumDocumentCharacters = 16 * 1024 * 1024;
    private const int MaximumElementDepth = 128;
    private static readonly XNamespace Namespace = XmlArchiveFormat.Namespace;
    private static readonly IReadOnlyCollection<ArchiveFormatDescriptor> Formats =
        Array.AsReadOnly(new[] { XmlArchiveFormat.Current });
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly ArchiveXmlJsonContext JsonContext = new(JsonOptions);
    private readonly IArchiveValidator validator;

    /// <summary>
    /// 初始化 XML 档案 codec。
    /// </summary>
    /// <param name="validator">格式无关的档案语义校验器。</param>
    public XmlArchiveCodec(IArchiveValidator validator)
    {
        ArgumentNullException.ThrowIfNull(validator);
        this.validator = validator;
    }

    /// <inheritdoc />
    public Uri CodecIdentifier => XmlArchiveFormat.CodecIdentifier;

    /// <inheritdoc />
    public IReadOnlyCollection<ArchiveFormatDescriptor> ReadableFormats => Formats;

    /// <inheritdoc />
    public IReadOnlyCollection<ArchiveFormatDescriptor> WritableFormats => Formats;

    /// <inheritdoc />
    public async ValueTask<ArchiveReadResult> ReadAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead)
        {
            throw new ArgumentException("XML 档案输入流必须可读。", nameof(source));
        }

        try
        {
            var settings = new XmlReaderSettings
            {
                Async = true,
                CloseInput = false,
                DtdProcessing = DtdProcessing.Prohibit,
                IgnoreComments = false,
                IgnoreProcessingInstructions = false,
                MaxCharactersFromEntities = 0,
                MaxCharactersInDocument = MaximumDocumentCharacters,
                XmlResolver = null
            };
            using var reader = XmlReader.Create(source, settings);
            var sourceXml = await XDocument.LoadAsync(
                reader,
                LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo,
                cancellationToken);
            EnsureDepthWithinLimit(sourceXml);

            var document = ParseDocument(sourceXml);
            var semanticValidation = validator.ValidateBundle(document.Bundle, ArchiveValidationScope.Import);
            if (semanticValidation.HasErrors)
            {
                return new ArchiveReadResult
                {
                    Validation = semanticValidation
                };
            }

            var knownXml = BuildDocument(document);
            var semanticFingerprint = Fingerprint(knownXml);
            var containsUnknown = !DocumentsEquivalent(sourceXml, knownXml);
            var roundTripState = new XmlArchiveRoundTripState(
                sourceXml,
                semanticFingerprint,
                containsUnknown);

            return new ArchiveReadResult
            {
                Document = document with
                {
                    SourceFormat = XmlArchiveFormat.Current,
                    RoundTripState = roundTripState
                },
                Validation = semanticValidation
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (UnsupportedXmlArchiveVersionException)
        {
            return InvalidRead(
                XmlArchiveValidationCodes.UnsupportedVersion,
                "该 XML 档案版本不受当前程序支持。",
                ArchiveValidationCategory.Compatibility);
        }
        catch (UnsupportedXmlArchiveResourceException)
        {
            return InvalidRead(
                XmlArchiveValidationCodes.UnsupportedResource,
                "XML 档案包含当前程序不支持的资源类型。",
                ArchiveValidationCategory.Compatibility);
        }
        catch (Exception exception) when (IsInvalidDocumentFailure(exception))
        {
            return InvalidRead(
                XmlArchiveValidationCodes.InvalidDocument,
                "XML 档案结构无效，无法安全读取。",
                exception is XmlException ? ArchiveValidationCategory.Security : ArchiveValidationCategory.Structure);
        }
    }

    /// <inheritdoc />
    public async ValueTask<ArchiveWriteResult> WriteAsync(
        ArchiveWriteRequest request,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite)
        {
            throw new ArgumentException("XML 档案输出流必须可写。", nameof(destination));
        }

        if (!IsCurrentFormat(request.TargetFormat))
        {
            return InvalidWrite(
                request.TargetFormat,
                Issue(
                    XmlArchiveValidationCodes.UnsupportedTargetFormat,
                    "请求的目标格式或版本不是当前 XML codec 支持的格式。",
                    ArchiveValidationCategory.Compatibility));
        }

        var semanticValidation = validator.ValidateBundle(
            request.Document.Bundle,
            ArchiveValidationScope.Export);
        if (semanticValidation.HasErrors)
        {
            return new ArchiveWriteResult
            {
                TargetFormat = request.TargetFormat,
                Validation = semanticValidation
            };
        }

        try
        {
            var knownXml = BuildDocument(request.Document);
            var output = SelectRoundTripOutput(request.Document, knownXml, out var compatibilityIssue);
            if (compatibilityIssue is not null)
            {
                return InvalidWrite(
                    request.TargetFormat,
                    semanticValidation.Issues.Concat(new[] { compatibilityIssue }).ToArray());
            }

            await using var buffer = new MemoryStream();
            var settings = new XmlWriterSettings
            {
                Async = true,
                CloseOutput = false,
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                Indent = true,
                NewLineHandling = NewLineHandling.Entitize,
                OmitXmlDeclaration = false
            };
            using (var writer = XmlWriter.Create(buffer, settings))
            {
                await output!.SaveAsync(writer, cancellationToken);
                await writer.FlushAsync();
            }

            buffer.Position = 0;
            await buffer.CopyToAsync(destination, cancellationToken);
            return new ArchiveWriteResult
            {
                TargetFormat = request.TargetFormat,
                Validation = semanticValidation
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (UnsupportedXmlArchiveResourceException)
        {
            return InvalidWrite(
                request.TargetFormat,
                semanticValidation.Issues.Concat(new[]
                {
                    Issue(
                        XmlArchiveValidationCodes.UnsupportedResource,
                        "档案包含当前 XML codec 不支持的资源类型。",
                        ArchiveValidationCategory.Compatibility)
                }).ToArray());
        }
        catch (Exception exception) when (IsInvalidDocumentFailure(exception))
        {
            return InvalidWrite(
                request.TargetFormat,
                semanticValidation.Issues.Concat(new[]
                {
                    Issue(
                        XmlArchiveValidationCodes.InvalidDocument,
                        "档案无法转换为有效的 XML 文档。",
                        ArchiveValidationCategory.Structure)
                }).ToArray());
        }
    }

    private static ArchiveDocument ParseDocument(XDocument document)
    {
        var root = document.Root ?? throw new InvalidDataException("XML 档案缺少根元素。");
        if (root.Name != Namespace + "ArchiveDocument")
        {
            throw new InvalidDataException("XML 档案根元素或命名空间无效。");
        }

        var version = RequiredAttribute(root, "formatVersion");
        if (!string.Equals(version, XmlArchiveFormat.CurrentVersion, StringComparison.Ordinal))
        {
            throw new UnsupportedXmlArchiveVersionException();
        }

        var bundleElement = RequiredElement(root, "Bundle");
        var producerElement = RequiredElement(bundleElement, "Producer");
        var entriesElement = RequiredElement(bundleElement, "Entries");
        var extensionsElement = bundleElement.Element(Namespace + "Extensions");
        var extensions = extensionsElement is null
            ? Array.Empty<ArchiveExtension>()
            : DeserializeExtensions(ReadJsonElement(extensionsElement));
        var entries = entriesElement.Elements(Namespace + "Entry")
            .Select(ParseResource)
            .ToArray();

        return new ArchiveDocument
        {
            Bundle = new ArchiveBundle
            {
                BundleId = new ArchiveBundleId(ParseGuid(bundleElement, "id")),
                BundleType = ParseEnum<ArchiveBundleType>(RequiredAttribute(bundleElement, "type")),
                CreatedAt = ParseDateTimeOffset(bundleElement, "createdAt"),
                Producer = new ApplicationIdentity(
                    ParseAbsoluteUri(producerElement, "identifier"),
                    RequiredAttribute(producerElement, "name"),
                    RequiredAttribute(producerElement, "version")),
                Extensions = extensions,
                Entries = entries
            }
        };
    }

    private static XDocument BuildDocument(ArchiveDocument document)
    {
        var bundle = document.Bundle;
        var root = new XElement(
            Namespace + "ArchiveDocument",
            new XAttribute("formatVersion", XmlArchiveFormat.CurrentVersion),
            new XElement(
                Namespace + "Bundle",
                new XAttribute("id", bundle.BundleId.Value.ToString("D")),
                new XAttribute("type", bundle.BundleType.ToString()),
                new XAttribute("createdAt", bundle.CreatedAt.ToString("O")),
                new XElement(
                    Namespace + "Producer",
                    new XAttribute("identifier", bundle.Producer.Identifier.AbsoluteUri),
                    new XAttribute("name", bundle.Producer.Name),
                    new XAttribute("version", bundle.Producer.Version)),
                WriteJsonElement(
                    "Extensions",
                    JsonSerializer.SerializeToElement(bundle.Extensions.ToArray(), JsonContext.ArchiveExtensionArray)),
                new XElement(
                    Namespace + "Entries",
                    bundle.Entries.Select(WriteResource))));
        return new XDocument(new XDeclaration("1.0", "utf-8", null), root);
    }

    private static XElement WriteResource(IArchiveResource resource)
    {
        var payload = SerializeResource(resource);
        return new XElement(
            Namespace + "Entry",
            new XAttribute("resourceType", resource.ResourceType.Value),
            WriteJsonElement("Content", payload, "ResourceType"));
    }

    private static IArchiveResource ParseResource(XElement entry)
    {
        var resourceType = RequiredAttribute(entry, "resourceType");
        var payload = ReadJsonElement(RequiredElement(entry, "Content"));
        return resourceType switch
        {
            "Patient" => payload.Deserialize(JsonContext.PatientResource)
                ?? throw new JsonException("Patient 资源为空。"),
            "Consultation" => payload.Deserialize(JsonContext.ConsultationResource)
                ?? throw new JsonException("Consultation 资源为空。"),
            "EnergyAssessment" => payload.Deserialize(JsonContext.EnergyAssessmentResource)
                ?? throw new JsonException("EnergyAssessment 资源为空。"),
            "DriAssessment" => payload.Deserialize(JsonContext.DriAssessmentResource)
                ?? throw new JsonException("DriAssessment 资源为空。"),
            "DietaryRecall" => payload.Deserialize(JsonContext.DietaryRecallResource)
                ?? throw new JsonException("DietaryRecall 资源为空。"),
            "SoapNote" => payload.Deserialize(JsonContext.SoapNoteResource)
                ?? throw new JsonException("SoapNote 资源为空。"),
            "NutritionAdvice" => payload.Deserialize(JsonContext.NutritionAdviceResource)
                ?? throw new JsonException("NutritionAdvice 资源为空。"),
            "NutritionReport" => payload.Deserialize(JsonContext.NutritionReportResource)
                ?? throw new JsonException("NutritionReport 资源为空。"),
            "NutritionScaleAssessment" => payload.Deserialize(JsonContext.NutritionScaleAssessmentResource)
                ?? throw new JsonException("NutritionScaleAssessment 资源为空。"),
            _ => throw new UnsupportedXmlArchiveResourceException()
        };
    }

    private static JsonElement SerializeResource(IArchiveResource resource) => resource switch
    {
        PatientResource patient => JsonSerializer.SerializeToElement(patient, JsonContext.PatientResource),
        ConsultationResource consultation => JsonSerializer.SerializeToElement(
            consultation,
            JsonContext.ConsultationResource),
        EnergyAssessmentResource energy => JsonSerializer.SerializeToElement(
            energy,
            JsonContext.EnergyAssessmentResource),
        DriAssessmentResource dri => JsonSerializer.SerializeToElement(dri, JsonContext.DriAssessmentResource),
        DietaryRecallResource recall => JsonSerializer.SerializeToElement(
            recall,
            JsonContext.DietaryRecallResource),
        SoapNoteResource soap => JsonSerializer.SerializeToElement(soap, JsonContext.SoapNoteResource),
        NutritionAdviceResource advice => JsonSerializer.SerializeToElement(
            advice,
            JsonContext.NutritionAdviceResource),
        NutritionReportResource report => JsonSerializer.SerializeToElement(
            report,
            JsonContext.NutritionReportResource),
        NutritionScaleAssessmentResource scale => JsonSerializer.SerializeToElement(
            scale,
            JsonContext.NutritionScaleAssessmentResource),
        _ => throw new UnsupportedXmlArchiveResourceException()
    };

    private static ArchiveExtension[] DeserializeExtensions(JsonElement element) =>
        element.Deserialize(JsonContext.ArchiveExtensionArray) ?? [];

    private static XElement WriteJsonElement(
        string name,
        JsonElement value,
        string? ignoredObjectProperty = null)
    {
        var element = new XElement(Namespace + name);
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                element.SetAttributeValue("kind", "object");
                foreach (var property in value.EnumerateObject())
                {
                    if (!string.Equals(property.Name, ignoredObjectProperty, StringComparison.Ordinal))
                    {
                        element.Add(WriteJsonElement(property.Name, property.Value));
                    }
                }

                break;
            case JsonValueKind.Array:
                element.SetAttributeValue("kind", "array");
                foreach (var item in value.EnumerateArray())
                {
                    element.Add(WriteJsonElement("Item", item));
                }

                break;
            case JsonValueKind.String:
                element.SetAttributeValue("kind", "string");
                element.Value = value.GetString() ?? string.Empty;
                break;
            case JsonValueKind.Number:
                element.SetAttributeValue("kind", "number");
                element.Value = value.GetRawText();
                break;
            case JsonValueKind.True:
            case JsonValueKind.False:
                element.SetAttributeValue("kind", "boolean");
                element.Value = value.GetBoolean() ? "true" : "false";
                break;
            case JsonValueKind.Null:
                element.SetAttributeValue("kind", "null");
                break;
            default:
                throw new JsonException("XML 档案包含无法表示的 JSON 值。");
        }

        return element;
    }

    private static JsonElement ReadJsonElement(XElement element)
    {
        var node = ReadJsonNode(element);
        using var document = JsonDocument.Parse(node?.ToJsonString() ?? "null");
        return document.RootElement.Clone();
    }

    private static JsonNode? ReadJsonNode(XElement element)
    {
        var kind = element.Attribute("kind")?.Value;
        return kind switch
        {
            "object" => ReadJsonObject(element),
            "array" => ReadJsonArray(element),
            "string" => JsonValue.Create(element.Value),
            "number" => JsonNode.Parse(element.Value),
            "boolean" => JsonValue.Create(bool.Parse(element.Value)),
            "null" => null,
            _ => throw new InvalidDataException("XML 值节点缺少有效的 kind 属性。")
        };
    }

    private static JsonObject ReadJsonObject(XElement element)
    {
        var result = new JsonObject();
        foreach (var child in element.Elements())
        {
            if (child.Name.Namespace != Namespace || child.Attribute("kind") is null)
            {
                continue;
            }

            result.Add(child.Name.LocalName, ReadJsonNode(child));
        }

        return result;
    }

    private static JsonArray ReadJsonArray(XElement element)
    {
        var result = new JsonArray();
        foreach (var child in element.Elements(Namespace + "Item"))
        {
            if (child.Attribute("kind") is not null)
            {
                result.Add(ReadJsonNode(child));
            }
        }

        return result;
    }

    private static XDocument? SelectRoundTripOutput(
        ArchiveDocument document,
        XDocument knownXml,
        out ArchiveValidationIssue? issue)
    {
        issue = null;
        if (!document.ContainsUnknownContent)
        {
            return knownXml;
        }

        if (document.RoundTripState is XmlArchiveRoundTripState state &&
            state.CodecIdentifier == XmlArchiveFormat.CodecIdentifier &&
            string.Equals(state.SemanticFingerprint, Fingerprint(knownXml), StringComparison.Ordinal))
        {
            return new XDocument(state.Source);
        }

        issue = Issue(
            XmlArchiveValidationCodes.UnknownContentConflict,
            "档案包含未知 XML 内容，且已知语义已经改变；为避免静默丢失内容，当前文档未写出。",
            ArchiveValidationCategory.Compatibility);
        return null;
    }

    private static bool DocumentsEquivalent(XDocument left, XDocument right) => string.Equals(
        Normalize(left).ToString(SaveOptions.DisableFormatting),
        Normalize(right).ToString(SaveOptions.DisableFormatting),
        StringComparison.Ordinal);

    private static string Fingerprint(XDocument document)
    {
        var normalized = Normalize(document).ToString(SaveOptions.DisableFormatting);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }

    private static XDocument Normalize(XDocument document) => new(
        document.Root is null ? null : Normalize(document.Root));

    private static XElement Normalize(XElement element) => new(
        element.Name,
        element.Attributes()
            .Where(attribute => !attribute.IsNamespaceDeclaration)
            .OrderBy(attribute => attribute.Name.NamespaceName, StringComparer.Ordinal)
            .ThenBy(attribute => attribute.Name.LocalName, StringComparer.Ordinal)
            .Select(attribute => new XAttribute(attribute.Name, attribute.Value)),
        element.Nodes().SelectMany(node => node switch
        {
            XElement child => new XNode[] { Normalize(child) },
            XText text when !string.IsNullOrWhiteSpace(text.Value) => new XNode[] { new XText(text.Value) },
            XCData cdata => new XNode[] { new XCData(cdata.Value) },
            XComment comment => new XNode[] { new XComment(comment.Value) },
            XProcessingInstruction instruction => new XNode[]
            {
                new XProcessingInstruction(instruction.Target, instruction.Data)
            },
            _ => Array.Empty<XNode>()
        }));

    private static void EnsureDepthWithinLimit(XDocument document)
    {
        if (document.Descendants().Any(element => element.Ancestors().Take(MaximumElementDepth + 1).Count() > MaximumElementDepth))
        {
            throw new XmlException("XML 档案元素嵌套过深。");
        }
    }

    private static bool IsCurrentFormat(ArchiveFormatDescriptor format) =>
        format.Identifier == XmlArchiveFormat.FormatIdentifier &&
        string.Equals(format.Version, XmlArchiveFormat.CurrentVersion, StringComparison.Ordinal);

    private static string RequiredAttribute(XElement element, string name) =>
        element.Attribute(name)?.Value is { Length: > 0 } value
            ? value
            : throw new InvalidDataException($"XML 元素 {element.Name.LocalName} 缺少必要属性。");

    private static XElement RequiredElement(XElement parent, string name) =>
        parent.Element(Namespace + name)
        ?? throw new InvalidDataException($"XML 元素 {parent.Name.LocalName} 缺少必要子元素。");

    private static Guid ParseGuid(XElement element, string attribute) =>
        Guid.TryParse(RequiredAttribute(element, attribute), out var value) && value != Guid.Empty
            ? value
            : throw new FormatException("XML 档案包含无效 UUID。");

    private static Uri ParseAbsoluteUri(XElement element, string attribute) =>
        Uri.TryCreate(RequiredAttribute(element, attribute), UriKind.Absolute, out var value)
            ? value
            : throw new FormatException("XML 档案包含无效 URI。");

    private static DateTimeOffset ParseDateTimeOffset(XElement element, string attribute) =>
        DateTimeOffset.TryParse(
            RequiredAttribute(element, attribute),
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind,
            out var value)
            ? value
            : throw new FormatException("XML 档案包含无效日期时间。");

    private static TEnum ParseEnum<TEnum>(string value)
        where TEnum : struct, Enum => Enum.TryParse<TEnum>(value, ignoreCase: false, out var result) &&
                                             Enum.IsDefined(result)
            ? result
            : throw new FormatException("XML 档案包含无效枚举值。");

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = false
        };
        options.Converters.Add(new ArchiveValueJsonConverter());
        return options;
    }

    private static ArchiveReadResult InvalidRead(
        string code,
        string message,
        ArchiveValidationCategory category) => new()
        {
            Validation = new ArchiveValidationResult
            {
                Issues = [Issue(code, message, category, ArchiveValidationSeverity.Fatal)]
            }
        };

    private static ArchiveWriteResult InvalidWrite(
        ArchiveFormatDescriptor targetFormat,
        params ArchiveValidationIssue[] issues) => new()
        {
            TargetFormat = targetFormat,
            Validation = new ArchiveValidationResult { Issues = issues }
        };

    private static ArchiveValidationIssue Issue(
        string code,
        string message,
        ArchiveValidationCategory category,
        ArchiveValidationSeverity severity = ArchiveValidationSeverity.Error) => new()
        {
            Code = code,
            Severity = severity,
            Category = category,
            Message = message,
            Path = new ArchiveElementPath("/Document")
        };

    private static bool IsInvalidDocumentFailure(Exception exception) => exception is
        XmlException or
        JsonException or
        InvalidDataException or
        FormatException or
        ArgumentException;

    private sealed class UnsupportedXmlArchiveVersionException : Exception;

    private sealed class UnsupportedXmlArchiveResourceException : Exception;
}
