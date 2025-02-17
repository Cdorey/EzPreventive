using System.Collections.Concurrent;

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

        public static ConcurrentDictionary<decimal, StandardTower> Dictionary { get; }

        public static StandardTower? GetStandardTower(decimal age)
        {
            var ageKey = Dictionary.Keys.Where(x => (x <= age)).OrderByDescending(x => x);
            return ageKey.Any() ? Dictionary[ageKey.First()] : null;
        }

        static StandardTower()
        {
            Dictionary = new ConcurrentDictionary<decimal, StandardTower>
            {
                [0.5m] = new StandardTower(["不建议额外添加", "0-10g", null, null, null, null, null, null, "15-50g（至少1个鸡蛋黄）", "25-75g", null, null, "25-100g", "25-100g", "700-500ml", "20-75g", null, null, null,]),
                [1] = new StandardTower(["0-1.5g", "5-15g", null, null, null, null, null, null, "25-50g", "50-75g", null, null, "50-150g", "50-150g", "600-400ml", "50-100g", null, null, null,]),
                [2] = new StandardTower(["<2g", "10-20g", null, "350-500g", null, "5-15g（适当加工）", "/", null, "50g", "50-75g", null, null, "100-200g", "100-200g", null, "75-125g", null, "适量", "600-700ml",]),
                [4] = new StandardTower(["<3g", "20-25g", null, "350-500g", null, "15-20g（适当加工）", "适量（适当加工）", null, "50g", "50-75g", null, null, "150-300g", "150-250g", null, "100-150g", null, "适量", "700-800ml",]),
                [6] = new StandardTower(["<4g", "20-25g", "300g", null, null, "105g每周", "50g每周", null, "25-40g", null, "40g", "40g", "300g", "150-200g", null, "150-200g", "30-70g", "25-50g", "800-1000ml",]),
                [11] = new StandardTower(["<5g", "25-30g", "300g", null, null, "105g每周", "50-70g每周", null, "40-50g", null, "50g", "50g", "400-450g", "200-300g", null, "225-250g", "30-70g", "25-50g", "1100-1300ml",]),
                [14] = new StandardTower(["<5g", "25-30g", "300g", null, null, "105-175g每周", "50-70g每周", null, "50g", null, "50-75g", "50-75g", "450-500g", "300-350g", null, "250-300g", "50-100g", "50-100g", "1200-1400ml",]),
                [18] = new StandardTower(["<5g", "25-30g", "300-500g", null, "25-35g", null, null, "120-200g", "1个", null, null, "每周至少两次", "300-500g", "200-350g", null, "200-300g", "50-150g", "50-100g", "1500-1700ml",])
            };
        }
    }
}