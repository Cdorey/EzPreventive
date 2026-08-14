using System.Text.Json.Serialization;
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
[JsonSerializable(typeof(ArchiveExtension[]))]
internal sealed partial class ArchiveXmlJsonContext : JsonSerializerContext;
