using EzNutrition.Application.Archives;
using EzNutrition.Domain.Assessments;
using EzNutrition.Domain.Consultations;
using EzNutrition.Domain.Dietary;

namespace EzNutrition.Application.Consultations
{
    /// <summary>
    /// 聚合一次营养咨询在各工作步骤中的运行态对象和状态。
    /// </summary>
    public class ConsultationWorkspace
    {
        private readonly IClient client;

        public ConsultationWorkspace(IClient client, ArchiveContractIdentity? contractIdentity = null)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            ContractIdentity = contractIdentity ?? ArchiveContractIdentity.Create();
        }

        /// <summary>
        /// 使用既有患者身份建立一次新的独立咨询。
        /// </summary>
        public ConsultationWorkspace(IClient client, ArchivePatientContext patientContext)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            ArgumentNullException.ThrowIfNull(patientContext);
            ExistingPatient = patientContext.SourcePatient;
            ContractIdentity = ArchiveContractIdentity.CreateForPatient(ExistingPatient);
        }

        internal object AiAdviceSyncRoot { get; } = new();

        internal EzNutrition.Archives.Contracts.Resources.PatientResource? ExistingPatient { get; }

        public bool ClientInfoFormEnabled { get; set; } = true;

        public bool IsLoading { get; set; }

        public IClient Client => client;

        /// <summary>
        /// 获取本次运行态咨询映射到档案契约时使用的稳定身份。
        /// </summary>
        public ArchiveContractIdentity ContractIdentity { get; }

        /// <summary>获取当前工作区是否为既有患者的新一次咨询。</summary>
        public bool IsFollowUp => ExistingPatient is not null;

        public EnergyCalculator? CurrentEnergyCalculator { get; set; }

        public DRIs? DRIs { get; set; }

        public DietaryRecallSurvey? DietaryRecallSurvey { get; set; }

        public DietaryTower? DietaryTower { get; set; }

        public AiGeneratedAdvice? AiGeneratedAdvice { get; set; }

        public EzNutrition.Shared.Data.DTO.PromptDto.PromptDto? AdvicePrompt { get; set; }

        public SubjectiveObjectiveAssessmentPlanInformation? SubjectiveObjectiveAssessmentPlanInformation { get; set; }
    }
}
