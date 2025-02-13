namespace EzNutrition.Shared.Data.DietaryRecallSurvey
{
    public class DietaryTower : Dictionary<string, decimal>
    {
        private static decimal ConvertedWeight(DietaryRecallEntry originalRecord)
        {
            if (originalRecord.Food.FoodGroups == "奶及奶制品")
            {
                var edibleWeight = originalRecord.IsAllEdible ? originalRecord.Weight : (originalRecord.Weight * (originalRecord.Food.EdiblePortion ?? 100) / 100);
                var atProtein = originalRecord.Food.FoodNutrientValues!.First(x => x.Nutrient?.FriendlyName == "蛋白质").Value / 100 * edibleWeight;
                return atProtein * 100 / 3;
            }
            else if (originalRecord.Food.FoodGroups == "大豆及坚果类" && originalRecord.Food?.FriendlyCode?[..2] == "03")
            {
                var edibleWeight = originalRecord.IsAllEdible ? originalRecord.Weight : (originalRecord.Weight * (originalRecord.Food.EdiblePortion ?? 100) / 100);
                var atProtein = originalRecord.Food.FoodNutrientValues!.First(x => x.Nutrient?.FriendlyName == "蛋白质").Value / 100 * edibleWeight;
                return atProtein * 100 / 35.1m;
            }
            else
            {
                return originalRecord.Weight;
            }
        }

        public DietaryTower(IEnumerable<DietaryRecallEntry> dietaryRecallEntries)
        {
            foreach (var entry in dietaryRecallEntries)
            {
                if (entry.Food.FoodGroups is not null)
                {
                    if (!ContainsKey(entry.Food.FoodGroups))
                    {
                        this[entry.Food.FoodGroups] = 0;
                    }
                    this[entry.Food.FoodGroups] += ConvertedWeight(entry);
                }
            }
        }
    }
}