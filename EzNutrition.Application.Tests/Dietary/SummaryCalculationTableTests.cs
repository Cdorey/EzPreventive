using EzNutrition.Domain.Dietary;
using EzNutrition.Shared.Data.Entities;
using System.Data;

namespace EzNutrition.Application.Tests.Dietary;

public sealed class SummaryCalculationTableTests
{
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
    public async Task CalculationDetailUsesTheSameEdibleWeightAdjustmentAsTheSummary()
    {
        var energy = Nutrient(1, "能量", "kCal");
        var food = Food("测试食物", ediblePortion: 75, (energy, 240m));
        var entries = new List<DietaryRecallEntry>
        {
            Entry(food, 80m, MealOccasion.Dinner, isAllEdible: false)
        };
        var calculation = new SummaryCalculationTable(entries, [energy]);

        var detail = await calculation.ToCalculateDataTableAsync();

        var row = Assert.Single(detail.Rows.Cast<DataRow>());
        Assert.Equal("测试食物", row["原料名称"]);
        Assert.Equal("80", row["原料原始重量"]);
        Assert.Equal("False", row["均为可食部"]);
        Assert.Equal(144m, decimal.Parse((string)row["能量"]));
        Assert.Equal(144m, calculation.TotalEnergy);
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
