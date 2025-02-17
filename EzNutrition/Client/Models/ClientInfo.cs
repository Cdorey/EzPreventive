using EzNutrition.Shared.Data.Entities;

namespace EzNutrition.Client.Models
{
    public class ClientInfo : IClient
    {
        private string? name;

        public event EventHandler? NameChanged;

        public string? Name
        {
            get => name;
            set
            {
                name = value;
                NameChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public string? Gender { get; set; }

        public int Age { get; set; } = 25;

        ////public decimal? PAL { get; set; }

        public decimal? Height { get; set; }

        public decimal? Weight { get; set; }

        public Guid ClientId { get; set; } = Guid.NewGuid();

        public string SpecialPhysiologicalPeriod { get; set; } = string.Empty;

    }
}
