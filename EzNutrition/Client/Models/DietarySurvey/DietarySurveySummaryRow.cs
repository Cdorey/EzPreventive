namespace EzNutrition.Client.Models.DietarySurvey
{
    public class DietarySurveySummaryRow
    {
        public string Abbreviation { get; set; } = string.Empty;

        public required string FriendlyName { get; set; }

        public required string ValueString { get; set; }

        public string Unit { get; set; } = string.Empty;

        public string? Flag { get; set; }

        public string ReferenceRange { get; set; } = string.Empty;

        public bool Expanded { get; set; } = false;

        public bool Expandable { get; set; } = false;

        public string ExpendTitle { get; set; } = "详细信息";

        public (string, string)[]? ExpendDescriptions { get; set; }
    }
}