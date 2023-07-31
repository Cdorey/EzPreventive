namespace EzNutrition.Server.Data.Entities
{
    public class Advice
    {
        public int AdviceId { get; set; }

        public string? Content { get; set; }

        public int Priority { get; set; }

        public List<Disease>? Diseases { get; set; }
    }
}
