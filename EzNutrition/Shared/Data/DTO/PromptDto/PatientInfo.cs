namespace EzNutrition.Shared.Data.DTO.PromptDto
{
    public class PatientInfo
    {
        public required string Gender { get; set; }

        public required long Age { get; set; }

        public required double BMI { get; set; }

        public required double PAL { get; set; }
    }
}