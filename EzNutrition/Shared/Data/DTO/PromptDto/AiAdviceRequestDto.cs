using System.ComponentModel.DataAnnotations;

namespace EzNutrition.Shared.Data.DTO.PromptDto;

/// <summary>
/// Describes the consultation data disclosed by the client when requesting AI advice.
/// Server-owned instructions are deliberately excluded from this transport contract.
/// </summary>
public sealed class AiAdviceRequestDto
{
    public const int CurrentSchemaVersion = 4;

    [Range(CurrentSchemaVersion, CurrentSchemaVersion)]
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required PatientInfo PatientInfo { get; init; }

    public DietaryRecallSurvey? DietaryRecallSurvey { get; init; }

    public ClinicalInfo? ClinicalInfo { get; init; }
}
