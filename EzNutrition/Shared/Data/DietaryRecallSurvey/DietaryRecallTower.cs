namespace EzNutrition.Shared.Data.DietaryRecallSurvey
{
    /// <summary>
    /// 这是一个根据膳食回顾调查核算的膳食宝塔，用于和标准膳食平衡宝塔比较
    /// </summary>
    public class DietaryRecallTower(StandardTower comparedWith) : DietaryTower
    {

        private readonly decimal[] towerValues = new decimal[8];
        private readonly static List<int> indexInStandardTower = [1, 2, 4, 7, 12, 13, 15, 17];

        private static decimal ConvertedWeight(DietaryRecallEntry originalRecord)
        {
            if (originalRecord.Food.FoodGroups == "奶制品")
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

        private static int IndexOf(string? foodGroupName)
        {
            return foodGroupName switch
            {
                "能量类" => 0,
                "奶及奶制品" => 1,
                "大豆及坚果类" => 2,
                "动物性食品" => 3,
                "蔬菜类" => 4,
                "水果类" => 5,
                "谷类" => 6,
                "薯类及淀粉制品" => 7,
                _ => -1,
            };
        }

        public override TowerLayer[] RenderTower()
        {
            var parentIndex = new int[19];
            ParentIndex.CopyTo(parentIndex, 0);

            for (int i = 0; i < comparedWith.TowerInfo.Length; i++)
            {
                if (comparedWith.TowerInfo[i] is null)
                {
                    continue;
                }
                if (string.IsNullOrWhiteSpace(comparedWith.TowerInfo[i]))
                {
                    continue;
                }
                while (parentIndex[i] != -1 && comparedWith.TowerInfo[parentIndex[i]] is null && !indexInStandardTower.Contains(i))
                {
                    parentIndex[i] = parentIndex[parentIndex[i]];
                }
            }

            var dictionary = new Dictionary<int, TowerLayer>();
            for (int i = 0; i < comparedWith.TowerInfo.Length; i++)
            {
                if (comparedWith.TowerInfo[i] is null && !indexInStandardTower.Contains(i))
                {
                    continue;
                }

                var recallTowerIndex = indexInStandardTower.IndexOf(i);
                if (parentIndex[i] == -1)
                {
                    dictionary[i] = new TowerLayer { LayerName = LayerName[i], StandardTowerValue = comparedWith.TowerInfo[i], DietaryRecallTower = recallTowerIndex == -1 ? null : $"{towerValues[recallTowerIndex]:0}g" };
                }
                else
                {
                    var childrenList = dictionary[parentIndex[i]].Children ?? [];
                    childrenList.Add(new TowerLayer { LayerName = LayerName[i], StandardTowerValue = comparedWith.TowerInfo[i], DietaryRecallTower = recallTowerIndex == -1 ? null : $"{towerValues[recallTowerIndex]:0}g" });
                    dictionary[parentIndex[i]].Children ??= childrenList;
                }
            }
            return [.. dictionary.Values];
        }

        public DietaryRecallTower(IEnumerable<DietaryRecallEntry> dietaryRecallEntries, StandardTower comparedWith) : this(comparedWith)
        {
            foreach (DietaryRecallEntry entry in dietaryRecallEntries)
            {
                var index = IndexOf(entry.Food.FoodGroups);
                if (entry.Food.FoodGroups == "奶" || entry.Food.FoodGroups == "奶制品")
                {
                    index = IndexOf("奶及奶制品");
                }
                if (index > -1)
                {
                    towerValues[index] += ConvertedWeight(entry);
                }
            }
        }
    }
}