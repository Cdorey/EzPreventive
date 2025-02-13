using EzNutrition.Shared.Data.DietaryRecallSurvey;
using EzNutrition.Shared.Data.Entities;
using System.Text.Json.Serialization;

namespace EzNutrition.Client.Models
{
    public class DietaryRecallSurvey(IClient client, IEnumerable<Food> foods,IEnumerable<Nutrient> nutrients,DRIs dRIs) : ITreatment
    {
        [JsonIgnore]
        public string[] Requirements { get; } = [];

        public IClient Client => client;

        [JsonIgnore]
        public IEnumerable<Food> Foods => foods;

        [JsonIgnore]
        public IEnumerable<Nutrient> Nutrients => nutrients;

        [JsonIgnore]
        public DRIs DRIs => dRIs;

        public List<DietaryRecallEntry> RecallEntries { get; } = [];

        public SummaryCalculationTable? SummaryCalculationTable { get; set; }
    }
}