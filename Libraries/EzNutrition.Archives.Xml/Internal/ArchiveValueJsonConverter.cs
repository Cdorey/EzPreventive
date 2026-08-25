using System.Text.Json;
using System.Text.Json.Serialization;
using EzNutrition.Archives.Contracts.ValueObjects;

namespace EzNutrition.Archives.Xml.Internal;

internal sealed class ArchiveValueJsonConverter : JsonConverter<ArchiveValue>
{
    public override ArchiveValue Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var kind = root.GetProperty("kind").GetString();
        var value = root.GetProperty("value");
        return kind switch
        {
            "text" => new TextArchiveValue(value.GetString() ?? string.Empty),
            "boolean" => new BooleanArchiveValue(value.GetBoolean()),
            "integer" => new IntegerArchiveValue(value.GetInt64()),
            "decimal" => new DecimalArchiveValue(value.GetDecimal()),
            "dateTime" => new DateTimeArchiveValue(value.GetDateTimeOffset()),
            "partialDate" => new PartialDateArchiveValue(value.Deserialize<PartialDate>(options)!),
            "coding" => new CodingArchiveValue(value.Deserialize<Coding>(options)!),
            "quantity" => new QuantityArchiveValue(value.Deserialize<Quantity>(options)!),
            "quantityRange" => new QuantityRangeArchiveValue(value.Deserialize<QuantityRange>(options)!),
            "logicalReference" => new LogicalReferenceArchiveValue(
                value.Deserialize<EzNutrition.Archives.Contracts.Identity.LogicalResourceReference>(options)!),
            "versionedReference" => new VersionedReferenceArchiveValue(
                value.Deserialize<EzNutrition.Archives.Contracts.Identity.VersionedResourceReference>(options)!),
            _ => throw new JsonException("无法识别档案值类型。")
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        ArchiveValue value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        switch (value)
        {
            case TextArchiveValue text:
                Write(writer, "text", text.Value, options);
                break;
            case BooleanArchiveValue boolean:
                Write(writer, "boolean", boolean.Value, options);
                break;
            case IntegerArchiveValue integer:
                Write(writer, "integer", integer.Value, options);
                break;
            case DecimalArchiveValue number:
                Write(writer, "decimal", number.Value, options);
                break;
            case DateTimeArchiveValue dateTime:
                Write(writer, "dateTime", dateTime.Value, options);
                break;
            case PartialDateArchiveValue partialDate:
                Write(writer, "partialDate", partialDate.Value, options);
                break;
            case CodingArchiveValue coding:
                Write(writer, "coding", coding.Value, options);
                break;
            case QuantityArchiveValue quantity:
                Write(writer, "quantity", quantity.Value, options);
                break;
            case QuantityRangeArchiveValue range:
                Write(writer, "quantityRange", range.Value, options);
                break;
            case LogicalReferenceArchiveValue logicalReference:
                Write(writer, "logicalReference", logicalReference.Value, options);
                break;
            case VersionedReferenceArchiveValue versionedReference:
                Write(writer, "versionedReference", versionedReference.Value, options);
                break;
            default:
                throw new JsonException("无法写出未知档案值类型。");
        }

        writer.WriteEndObject();
    }

    private static void Write<TValue>(
        Utf8JsonWriter writer,
        string kind,
        TValue value,
        JsonSerializerOptions options)
    {
        writer.WriteString("kind", kind);
        writer.WritePropertyName("value");
        JsonSerializer.Serialize(writer, value, options);
    }
}
