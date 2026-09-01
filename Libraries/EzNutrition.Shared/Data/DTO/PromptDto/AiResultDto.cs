namespace EzNutrition.Shared.Data.DTO.PromptDto
{
    public record AiResultDto(
        string Content,
        bool IsReasoningContent,
        bool IsError = false);

}
