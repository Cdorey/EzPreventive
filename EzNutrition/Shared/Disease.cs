namespace EzNutrition.Shared
{
    public class Disease
    {
        public int DiseaseId { get; set; }

        public string? FriendlyName { get; set; }

        public string? ICD10 { get; set; }

        public List<Advice>? Advices { get; set; }
    }

}
