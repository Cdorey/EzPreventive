using EzNutrition.Client.Models.DietarySurvey;
using EzNutrition.Shared.Data.DietaryRecallSurvey;
using EzNutrition.Client.Archives;

namespace EzNutrition.Client.Models
{
    public class Archive(IClient client, ArchiveContractIdentity? contractIdentity = null)
    {
        public bool ClientInfoFormEnabled { get; set; } = true;

        public bool IsLoading { get; set; }

        public IClient Client => client;

        /// <summary>
        /// 获取本次浏览器咨询映射到档案契约时使用的稳定身份。
        /// </summary>
        public ArchiveContractIdentity ContractIdentity { get; } = contractIdentity ?? ArchiveContractIdentity.Create();

        public EnergyCalculator? CurrentEnergyCalculator { get; set; }

        public DRIs? DRIs { get; set; }

        public DietaryRecallSurvey? DietaryRecallSurvey { get; set; }

        public DietaryTower? DietaryTower { get; set; }

        public AiGeneratedAdvice? AiGeneratedAdvice { get; set; }

        public EzNutrition.Shared.Data.DTO.PromptDto.PromptDto? AdvicePrompt { get; set; }

        public SubjectiveObjectiveAssessmentPlanInformation? SubjectiveObjectiveAssessmentPlanInformation { get; set; }
    }
}
