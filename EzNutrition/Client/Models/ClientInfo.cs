using EzNutrition.Shared.Data.Entities;

namespace EzNutrition.Client.Models
{
    public class ClientInfo
    {
        public List<EER> AvailableEERs { get; set; } = new();
        public List<DietaryReferenceIntakeValue> AvailableDRIs { get; set; } = new();

        public string? Name { get; set; }

        public string? Gender { get; set; }
        public int Age { get; set; } = 25;

        public decimal? PAL { get; set; }
        public decimal? Height { get; set; }
        public decimal? Weight { get; set; }
    }

}
