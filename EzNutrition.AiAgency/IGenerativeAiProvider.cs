using EzNutrition.Shared.Data.DTO.PromptDto;

namespace EzNutrition.AiAgency
{
    /// <summary>
    /// Provides metadata about the AI service in use.
    /// </summary>
    public interface IGenerativeAiProvider
    {
        /// <summary>
        /// Identifies the organization or service that supplies this AI.
        /// For example: "Tencent Cloud", "OpenAI", etc.
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Describes the underlying platform or environment
        /// on which the model is running.
        /// For example: "LKEAP on ap-shanghai", "Azure OpenAI", etc.
        /// </summary>
        string PlatformDetails { get; }

        /// <summary>
        /// Additional notes or disclaimers you wish to convey,
        /// such as the model version, usage limits, or disclaimers.
        /// </summary>
        string AdditionalInfo { get; }

        IAsyncEnumerable<AiResultDto> Generate(PromptDto prompt);
    }
}