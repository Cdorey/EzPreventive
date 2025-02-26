namespace EzNutrition.Shared.Data.DTO.PromptDto
{
    public class PromptDto
    {
        public required PatientInfo PatientInfo { get; set; }

        public DietaryRecallSurvey? DietaryRecallSurvey { get; set; }

        public ClinicalInfo? ClinicalInfo { get; set; }

        public DialogConfiguration? DialogConfiguration { get; set; }
    }
}