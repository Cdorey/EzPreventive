namespace EzNutrition.Shared.Data.DTO.PromptDto
{
    public class PatientInfo
    {
        public required string? Gender { get; set; }

        public required long Age { get; set; }

        public required decimal? BMI { get; set; }

        public required decimal? PAL { get; set; }

        public required decimal? Height { get; set; }

        public required decimal? Width { get; set; }

        public required int? TotalBalanceEnergyViaCalculationg { get; set; }

        public required string? SpecialPhysiologicalPeriod { get; set; }
    }
}