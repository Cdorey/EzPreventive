using EzNutrition.Application.Archives;
using EzNutrition.Domain.Assessments;
using EzNutrition.Domain.Consultations;
using EzNutrition.Domain.Dietary;

namespace EzNutrition.Application.Consultations
{
    /// <summary>
    /// 聚合一次营养咨询在各工作步骤中的运行态对象和状态。
    /// </summary>
    public class ConsultationWorkspace(IClient client, ArchiveContractIdentity? contractIdentity = null)
    {
        public bool ClientInfoFormEnabled { get; set; } = true;

        public bool IsLoading { get; set; }

        public IClient Client => client;

        /// <summary>
        /// 获取本次运行态咨询映射到档案契约时使用的稳定身份。
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
