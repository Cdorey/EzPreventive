namespace EzNutrition.Shared.Data.DietaryRecallSurvey
{
    /// <summary>
    /// 这是一个由营养学会定义的标准膳食平衡宝塔
    /// </summary>
    public class StandardTower(string?[] standardTower) : DietaryTower
    {
        public string?[] TowerInfo { get; } = standardTower.Length == 19 ? standardTower : throw new ArgumentOutOfRangeException(nameof(standardTower));

        public override TowerLayer[] RenderTower()
        {
            var parentIndex = new int[19];
            ParentIndex.CopyTo(parentIndex, 0);
            for (int i = 0; i < TowerInfo.Length; i++)
            {
                if (TowerInfo[i] is null)
                {
                    continue;
                }

                while (parentIndex[i] != -1 && TowerInfo[parentIndex[i]] is null)
                {
                    parentIndex[i] = parentIndex[parentIndex[i]];
                }
            }
            var dictionary = new Dictionary<int, TowerLayer>();
            for (int i = 0; i < TowerInfo.Length; i++)
            {
                if (TowerInfo[i] is null)
                {
                    continue;
                }

                if (parentIndex[i] == -1)
                {
                    dictionary[i] = new TowerLayer { LayerName = LayerName[i], StandardTowerValue = TowerInfo[i] };
                }
                else
                {
                    var childrenList = dictionary[parentIndex[i]].Children ?? [];
                    childrenList.Add(new TowerLayer { LayerName = LayerName[i], StandardTowerValue = TowerInfo[i] });
                    dictionary[parentIndex[i]].Children ??= childrenList;
                }
            }
            return [.. dictionary.Values];
        }
    }
}