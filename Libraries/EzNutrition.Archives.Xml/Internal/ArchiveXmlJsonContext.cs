using System.Text.Json.Serialization;
using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Archives.Contracts.ValueObjects;

namespace EzNutrition.Archives.Xml.Internal;

[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(PatientResource))]
[JsonSerializable(typeof(ConsultationResource))]
[JsonSerializable(typeof(EnergyAssessmentResource))]
[JsonSerializable(typeof(DriAssessmentResource))]
[JsonSerializable(typeof(DietaryRecallResource))]
[JsonSerializable(typeof(SoapNoteResource))]
[JsonSerializable(typeof(NutritionAdviceResource))]
[JsonSerializable(typeof(NutritionReportResource))]
[JsonSerializable(typeof(NutritionScaleAssessmentResource))]
[JsonSerializable(typeof(ArchiveExtension[]))]
// ArchiveValue 转换器会按运行时值类型再次调用序列化器，因此必须显式登记封闭联合的全部成员类型。
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(decimal))]
[JsonSerializable(typeof(DateTimeOffset))]
[JsonSerializable(typeof(PartialDate))]
[JsonSerializable(typeof(ChronologicalAge))]
[JsonSerializable(typeof(Coding))]
[JsonSerializable(typeof(Quantity))]
[JsonSerializable(typeof(QuantityRange))]
[JsonSerializable(typeof(LogicalResourceReference))]
[JsonSerializable(typeof(VersionedResourceReference))]
internal sealed partial class ArchiveXmlJsonContext : JsonSerializerContext;
