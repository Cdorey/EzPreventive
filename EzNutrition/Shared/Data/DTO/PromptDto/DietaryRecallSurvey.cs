namespace EzNutrition.Shared.Data.DTO.PromptDto
{
    public partial class DietaryRecallSurvey
    {
        public string[] ExcessiveNutrients { get; set; } = [];

        public string[] DeficientNutrients { get; set; } = [];
    }
}