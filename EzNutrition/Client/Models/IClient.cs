namespace EzNutrition.Client.Models
{
    public interface IClient
    {
        string? Name { get; set; }
        string? Gender { get; set; }
        int Age { get; set; }
        decimal? Height { get; set; }
        decimal? Weight { get; set; }
        string SpecialPhysiologicalPeriod { get; set; }
    }
}
