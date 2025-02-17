namespace EzNutrition.Client.Models
{
    public class Archive(IClient client)
    {
        public bool ClientInfoFormEnabled { get; set; } = true;

        public IClient Client => client;

        public EnergyCalculator? CurrentEnergyCalculator { get; set; }

        public DRIs? DRIs { get; set; }

        public DietaryRecallSurvey? DietaryRecallSurvey { get; set; }

    }
}
