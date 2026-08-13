using EzNutrition.Domain.Dietary;
using EzNutrition.Shared.Data.Entities;

namespace EzNutrition.Application.Tests.Dietary;

public sealed class SummaryCalculationTableTests
{
    [Theory]
    [InlineData(5, 10, 20, DietaryReferenceStatus.BelowRange)]
    [InlineData(15, 10, 20, DietaryReferenceStatus.WithinRange)]
    [InlineData(25, 10, 20, DietaryReferenceStatus.AboveRange)]
    [InlineData(15, null, null, DietaryReferenceStatus.NotEstablished)]
    public void NutrientAssessmentDerivesReferenceStatusFromNumericBounds(
        int value,
        int? lowerReference,
        int? upperReference,
        DietaryReferenceStatus expected)
    {
        var assessment = new DietaryNutrientAssessment
        {
            FriendlyName = "测试营养素",
            Value = value,
            LowerReference = lowerReference,
            UpperReference = upperReference
        };

        Assert.Equal(expected, assessment.ReferenceStatus);
    }

    [Fact]
    public void CalculatesEdibleAndReportedWeightsWithoutChangingTheirMeaning()
    {
        var energy = Nutrient(1, "能量", "kCal");
        var protein = Nutrient(2, "蛋白质", "g");
        var food = Food(
            "测试食物",
            ediblePortion: 60,
            (energy, 200m),
            (protein, 10m));
        var entries = new List<DietaryRecallEntry>
        {
            Entry(food, 100m, MealOccasion.Breakfast, isAllEdible: true),
            Entry(food, 100m, MealOccasion.Lunch, isAllEdible: false)
        };

        var calculation = new SummaryCalculationTable(entries, [energy, protein]);

        Assert.Equal(320m, calculation.TotalEnergy);
        Assert.Equal(16m, calculation[protein]);
        Assert.Equal(64m, calculation.ProteinEnergy);
        Assert.Equal(
            200m,
            Assert.Single(calculation[MealOccasion.Breakfast], value => value.NutrientId == energy.NutrientId).Value);
        Assert.Equal(
            120m,
            Assert.Single(calculation[MealOccasion.Lunch], value => value.NutrientId == energy.NutrientId).Value);
    }

    [Fact]
    public void CalculationDetailUsesTheSameEdibleWeightAdjustmentAsTheSummary()
    {
        var energy = Nutrient(1, "能量", "kCal");
        var food = Food("测试食物", ediblePortion: 75, (energy, 240m));
        var entries = new List<DietaryRecallEntry>
        {
            Entry(food, 80m, MealOccasion.Dinner, isAllEdible: false)
        };
        var calculation = new SummaryCalculationTable(entries, [energy]);

        var detail = calculation.CreateEntryCalculations();

        var row = Assert.Single(detail);
        Assert.Equal("测试食物", row.FoodName);
        Assert.Equal(80m, row.RecordedWeight);
        Assert.False(row.IsAllEdible);
        Assert.Equal(144m, row.NutrientValues[energy.NutrientId]);
        Assert.Equal(144m, calculation.TotalEnergy);
    }

    [Fact]
    public void AggregatesTotalsMealsFoodsRanksAndDetailsWithoutChangingResults()
    {
        var energy = Nutrient(1, "能量", "kCal");
        var protein = Nutrient(2, "蛋白质", "g");
        var fat = Nutrient(3, "脂肪", "g");
        var carbohydrate = Nutrient(4, "碳水化合物", "g");
        var nutrients = new[] { energy, protein, fat, carbohydrate };
        var pork = Food(
            "猪肉",
            ediblePortion: 80,
            (energy, 200m),
            (protein, 10m),
            (fat, 5m),
            (carbohydrate, 20m));
        var rice = Food(
            "米饭",
            ediblePortion: 100,
            (energy, 100m),
            (protein, 2m),
            (fat, 1m),
            (carbohydrate, 30m));
        var entries = new List<DietaryRecallEntry>
        {
            Entry(pork, 100m, MealOccasion.Breakfast, isAllEdible: true),
            Entry(pork, 50m, MealOccasion.Lunch, isAllEdible: false),
            Entry(rice, 200m, MealOccasion.Lunch, isAllEdible: true)
        };

        var calculation = new SummaryCalculationTable(entries, [.. nutrients]);

        Assert.Equal(480m, calculation.TotalEnergy);
        Assert.Equal(18m, calculation[protein]);
        Assert.Equal(9m, calculation[fat]);
        Assert.Equal(88m, calculation[carbohydrate]);
        Assert.Equal(72m, calculation.ProteinEnergy);
        Assert.Equal(81m, calculation.FatEnergy);
        Assert.Equal(352m, calculation.CarbohydrateEnergy);

        Assert.Equal(
            200m,
            Assert.Single(
                calculation[MealOccasion.Breakfast],
                value => value.NutrientId == energy.NutrientId).Value);
        Assert.Equal(
            280m,
            Assert.Single(
                calculation[MealOccasion.Lunch],
                value => value.NutrientId == energy.NutrientId).Value);
        Assert.Equal(
            14m,
            Assert.Single(
                calculation[pork],
                value => value.NutrientId == protein.NutrientId).Value);

        Assert.Collection(
            calculation.ProteinRank,
            value =>
            {
                Assert.Same(pork, value.Food);
                Assert.Equal(14m, value.Value);
            },
            value =>
            {
                Assert.Same(rice, value.Food);
                Assert.Equal(4m, value.Value);
            });
        Assert.Collection(
            calculation.CarbohydrateRank,
            value =>
            {
                Assert.Same(rice, value.Food);
                Assert.Equal(60m, value.Value);
            },
            value =>
            {
                Assert.Same(pork, value.Food);
                Assert.Equal(28m, value.Value);
            });

        Assert.Collection(
            calculation.CreateEntryCalculations(),
            row => Assert.Equal(200m, row.NutrientValues[energy.NutrientId]),
            row => Assert.Equal(80m, row.NutrientValues[energy.NutrientId]),
            row => Assert.Equal(200m, row.NutrientValues[energy.NutrientId]));
    }

    private static Nutrient Nutrient(int id, string name, string unit) => new()
    {
        NutrientId = id,
        FriendlyName = name,
        DefaultMeasureUnit = unit
    };

    private static Food Food(
        string name,
        int ediblePortion,
        params (Nutrient Nutrient, decimal Value)[] values)
    {
        var food = new Food
        {
            FoodId = Guid.NewGuid(),
            FriendlyCode = "TEST-001",
            FriendlyName = name,
            EdiblePortion = ediblePortion
        };
        food.FoodNutrientValues = values.Select(value => new FoodNutrientValue
        {
            Food = food,
            FoodId = food.FoodId,
            Nutrient = value.Nutrient,
            NutrientId = value.Nutrient.NutrientId,
            MeasureUnit = value.Nutrient.DefaultMeasureUnit,
            Value = value.Value
        }).ToList();
        return food;
    }

    private static DietaryRecallEntry Entry(
        Food food,
        decimal weight,
        MealOccasion meal,
        bool isAllEdible) => new()
        {
            Food = food,
            Weight = weight,
            MealOccasion = meal,
            IsAllEdible = isAllEdible
        };
}
