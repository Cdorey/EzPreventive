using EzNutrition.Client.Models.DietarySurvey;
using EzNutrition.Shared.Data.DietaryRecallSurvey;

namespace EzNutrition.Client.Models
{
    public class Archive(IClient client)
    {
        public bool ClientInfoFormEnabled { get; set; } = true;

        public IClient Client => client;

        public EnergyCalculator? CurrentEnergyCalculator { get; set; }

        public DRIs? DRIs { get; set; }

        public DietaryRecallSurvey? DietaryRecallSurvey { get; set; }

        public DietaryTower? DietaryTower { get; set; }

        public AiGeneratedAdvice? AiGeneratedAdvice { get; set; }

        public EzNutrition.Shared.Data.DTO.PromptDto.PromptDto? AdvicePrompt { get; set; }
    }
}
