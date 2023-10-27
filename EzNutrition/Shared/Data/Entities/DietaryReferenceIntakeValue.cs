namespace EzNutrition.Shared.Data.Entities
{
    public class DietaryReferenceIntakeValue
    {
        public decimal? AgeStart { get; set; }

        public string? Gender { get; set; }

        public string? SpecialPhysiologicalPeriod { get; set; }

        public string Nutrient { get; set; } = string.Empty;

        public DietaryReferenceIntakeType RecordType { get; set; }

        public bool IsOffset { get; set; } = false;

        public decimal Value { get; set; }

        public string MeasureUnit { get; set; } = string.Empty;

        public string? Detail { get; set; }
    }
}
