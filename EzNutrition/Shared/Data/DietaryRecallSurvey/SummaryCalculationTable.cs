using EzNutrition.Shared.Data.Entities;
using System.Collections;

namespace EzNutrition.Shared.Data.DietaryRecallSurvey
{
    public class SummaryCalculationTable(IEnumerable<DietaryRecallEntry> dietaryRecallEntries)
    {
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

        private static IEnumerable<FoodNutrientValue> ToFoodNutrientValue(DietaryRecallEntry dietaryRecallEntry)
        {
            var weight = dietaryRecallEntry.Weight;
            if (!dietaryRecallEntry.IsAllEdible)
            {
                weight = weight * (dietaryRecallEntry.Food.EdiblePortion ?? 100) / 100;
            }

            foreach (var foodNutrientValue in dietaryRecallEntry.Food.FoodNutrientValues!)
            {
                var nutrient = new Nutrient
                {
                    NutrientId = foodNutrientValue.NutrientId,
                    FriendlyName = foodNutrientValue.Nutrient!.FriendlyName,
                    DefaultMeasureUnit = foodNutrientValue.Nutrient?.DefaultMeasureUnit,
                };
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
    }
}