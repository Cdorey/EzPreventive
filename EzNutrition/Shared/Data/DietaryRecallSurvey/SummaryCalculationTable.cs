using EzNutrition.Shared.Data.Entities;
using System.Collections;
using System.Data;

namespace EzNutrition.Shared.Data.DietaryRecallSurvey
{
    public class SummaryCalculationTable(List<DietaryRecallEntry> dietaryRecallEntries, List<Nutrient> nutrients)
    {
        private IEnumerable<FoodNutrientValue> ToFoodNutrientValue(DietaryRecallEntry dietaryRecallEntry)
        {
            var weight = dietaryRecallEntry.Weight;
            if (!dietaryRecallEntry.IsAllEdible)
            {
                weight = weight * (dietaryRecallEntry.Food.EdiblePortion ?? 100) / 100;
            }

            foreach (var foodNutrientValue in dietaryRecallEntry.Food.FoodNutrientValues!)
            {
                var nutrient = nutrients.First(x => x.NutrientId == foodNutrientValue.NutrientId);
                yield return new FoodNutrientValue
                {
                    Food = dietaryRecallEntry.Food,
                    FoodId = dietaryRecallEntry.Food.FoodId,
                    Nutrient = nutrient,
                    NutrientId = nutrient.NutrientId,
                    MeasureUnit = foodNutrientValue.MeasureUnit ?? nutrient.DefaultMeasureUnit,
                    Value = foodNutrientValue.Value * weight / 100
                };
            }
        }

        public async Task<DataTable> ToCalculateDataTableAsync()
        {
            var table = new DataTable();
            await Task.Run(() =>
            {
                table.Columns.Add("原料名称");
                table.Columns.Add("原料原始重量");
                table.Columns.Add("均为可食部");
                foreach (var nutrient in nutrients)
                {
                    table.Columns.Add(nutrient.FriendlyName ?? string.Empty, typeof(string));
                }
                foreach (var entry in dietaryRecallEntries)
                {
                    var x = ToFoodNutrientValue(entry);
                    var values = from nutrient in nutrients
                                 select (x.FirstOrDefault(f => f.NutrientId == nutrient.NutrientId)?.Value ?? 0).ToString();
                    table.Rows.Add([entry.Food.FriendlyName ?? string.Empty, entry.Weight, entry.IsAllEdible, .. values]);
                }
            });
            return table;
        }

        /// <summary>
        /// 计算指定营养素的总值
        /// </summary>
        /// <param name="nutrient"></param>
        /// <returns></returns>
        public decimal this[Nutrient nutrient]
        {
            get
            {
                var records = from entry in dietaryRecallEntries
                              from nutrientValue in entry.Food.FoodNutrientValues!
                              where nutrientValue.Nutrient!.NutrientId == nutrient.NutrientId
                              select new { Entry = entry, nutrientValue.Value };

                return records.Sum(x =>
                {
                    if (x.Entry.IsAllEdible)
                    {
                        return x.Entry.Weight * x.Value / 100;
                    }
                    else
                    {
                        return (x.Entry.Weight * (x.Entry.Food.EdiblePortion ?? 100) / 100) * x.Value / 100;
                    }
                });
            }
        }

        public decimal this[string nutrientFriendlyName] => this[nutrients.First(x => x.FriendlyName == nutrientFriendlyName)];

        /// <summary>
        /// 计算指定食物在本次问卷中的各类营养素的总值
        /// </summary>
        /// <param name="food"></param>
        /// <returns></returns>
        public IEnumerable<FoodNutrientValue> this[Food food]
        {
            get
            {
                return from entry in dietaryRecallEntries
                       where entry.Food.FoodId == food.FoodId
                       from foodNutrientValue in ToFoodNutrientValue(entry)
                       group foodNutrientValue by foodNutrientValue.NutrientId into gp
                       select new FoodNutrientValue
                       {
                           Food = food,
                           FoodId = food.FoodId,
                           Nutrient = gp.First().Nutrient,
                           NutrientId = gp.First().NutrientId,
                           MeasureUnit = gp.First().MeasureUnit,
                           Value = gp.Sum(x => x.Value)
                       };
            }
        }

        /// <summary>
        /// 计算指定餐次的各类营养素的总值
        /// </summary>
        /// <param name="mealOccasion"></param>
        /// <returns></returns>
        public IEnumerable<FoodNutrientValue> this[MealOccasion mealOccasion]
        {
            get
            {
                return from entry in dietaryRecallEntries
                       where entry.MealOccasion == mealOccasion
                       from foodNutrientValue in ToFoodNutrientValue(entry)
                       group foodNutrientValue by foodNutrientValue.NutrientId into gp
                       select new FoodNutrientValue
                       {
                           Nutrient = gp.First().Nutrient,
                           NutrientId = gp.First().NutrientId,
                           MeasureUnit = gp.First().MeasureUnit,
                           Value = gp.Sum(x => x.Value)
                       };
            }
        }

        /// <summary>
        /// 总能量
        /// </summary>
        public decimal TotalEnergy => this["能量"];

        /// <summary>
        /// 碳水化合物供能
        /// </summary>
        public decimal CarbohydrateEnergy => this["碳水化合物"] * 4;

        /// <summary>
        /// 脂肪供能
        /// </summary>
        public decimal FatEnergy => this["脂肪"] * 9;

        /// <summary>
        /// 蛋白质供能
        /// </summary>
        public decimal ProteinEnergy => this["蛋白质"] * 4;

        public IEnumerable<FoodNutrientValue> CarbohydrateRank
        {
            get
            {
                return from entry in dietaryRecallEntries
                       group entry by entry.Food into gp
                       let sumValuesByFood = this[gp.Key]
                       from sumValueByFood in sumValuesByFood
                       where sumValueByFood.Nutrient!.FriendlyName == "碳水化合物"
                       orderby sumValueByFood.Value descending
                       select sumValueByFood;
            }
        }

        public IEnumerable<FoodNutrientValue> FatRank
        {
            get
            {
                return from entry in dietaryRecallEntries
                       group entry by entry.Food into gp
                       let sumValuesByFood = this[gp.Key]
                       from sumValueByFood in sumValuesByFood
                       where sumValueByFood.Nutrient!.FriendlyName == "脂肪"
                       orderby sumValueByFood.Value descending
                       select sumValueByFood;
            }
        }

        public IEnumerable<FoodNutrientValue> ProteinRank
        {
            get
            {
                return from entry in dietaryRecallEntries
                       group entry by entry.Food into gp
                       let sumValuesByFood = this[gp.Key]
                       from sumValueByFood in sumValuesByFood
                       where sumValueByFood.Nutrient!.FriendlyName == "蛋白质"
                       orderby sumValueByFood.Value descending
                       select sumValueByFood;
            }
        }

        //public DietaryTower DietaryTower { get; } = new DietaryRecallTower(dietaryRecallEntries, standartTower);
    }
}