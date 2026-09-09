using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Archives.Contracts.ValueObjects;
using EzNutrition.Domain.Dietary;
using EzNutrition.Presentation.Reports;
using EzNutrition.Shared.Data.Entities;

namespace EzNutrition.Client.Tests.Tests;

public sealed class DietaryReportNutrientsTests
{
    [Fact]
    public void Macronutrient_tables_sort_contributions_and_restart_ranks_with_ties()
    {
        var tables = DietaryReportPdfModel.CreateContributionTables([
            new() { FriendlyName = "碳水化合物", FoodContributions = [new("食材乙", 5, "g")] },
            new() { FriendlyName = "蛋白质", FoodContributions = [new("食材丙", 2, "g"), new("食材甲", 10, "g"), new("食材乙", 10, "g")] },
            new() { FriendlyName = "总脂肪", FoodContributions = [new("食材丙", 3, "g"), new("食材甲", 7, "g")] }
        ]);
        Assert.Equal(["蛋白质", "脂肪", "碳水化合物"], tables.Select(table => table.Title));
        Assert.Equal(["食材甲", "食材乙", "食材丙"], tables[0].Rows.Select(row => row[1]));
        Assert.Equal(["1", "1", "3"], tables[0].Rows.Select(row => row[0]));
        Assert.Equal(["1", "2"], tables[1].Rows.Select(row => row[0]));
        Assert.Equal(["1", "食材乙", "5 g"], Assert.Single(tables[2].Rows));
        var tied = new DietaryNutrientAssessment { FriendlyName = "蛋白质",
            FoodContributions = [new("甲", 10, "g"), new("乙", 10, "g"), new("丙", 5, "g")] };
        Assert.Equal(2, DietaryReportPdfModel.CreateContributionTables([tied], 1)[0].Rows.Length);
        Assert.Equal(2, DietaryReportPdfModel.CreateContributionTables([tied], 2)[0].Rows.Length);
        Assert.Equal(3, DietaryReportPdfModel.CreateContributionTables([tied], null)[0].Rows.Length);
    }

    [Theory]
    [InlineData(40, 55, 150, "↓", "55 g-150 g", "RNI-UL")]
    [InlineData(160, 55, 150, "↑", "55 g-150 g", "RNI-UL")]
    [InlineData(55, 55, 150, "", "55 g-150 g", "RNI-UL")]
    [InlineData(150, 55, 150, "", "55 g-150 g", "RNI-UL")]
    [InlineData(160, -1, 150, "↑", "≤150 g", "-UL")]
    [InlineData(60, 55, -1, "", "≥55 g", "RNI-")]
    [InlineData(60, -1, -1, "", "—", "—")]
    public void Ranges_and_markers_preserve_existing_bounds(int amount, int lower, int upper,
        string marker, string range, string types)
    {
        var row = Row(new()
        {
            FriendlyName = "蛋白质", Value = amount, Unit = "g",
            LowerReference = lower < 0 ? null : new(DietaryReferenceIntakeType.RNI, lower, "g"),
            UpperReference = upper < 0 ? null : new(DietaryReferenceIntakeType.UL, upper, "g")
        });
        Assert.Equal(marker, row.Marker);
        Assert.Equal(range, row.ReferenceRange);
        Assert.Equal(types, row.ReferenceTypes);
    }

    [Fact]
    public void Ai_amdr_and_context_references_keep_their_distinct_roles()
    {
        var ai = Row(new() { FriendlyName = "总维生素E", Value = 15, Unit = "mg",
            LowerReference = new(DietaryReferenceIntakeType.AI, 14, "mg/d") });
        Assert.Equal("AI-", ai.ReferenceTypes);
        Assert.Equal("≥14 mg/d", ai.ReferenceRange);
        var ratio = Row(new() { FriendlyName = "蛋白质供能比", Value = 30, Unit = "%E",
            LowerReference = new(DietaryReferenceIntakeType.AMDR_L, 10, "%E"),
            UpperReference = new(DietaryReferenceIntakeType.AMDR_H, 20, "%E") });
        Assert.Equal("AMDR_L-AMDR_H", ratio.ReferenceTypes);
        Assert.Equal("10 %E-20 %E", ratio.ReferenceRange);
        Assert.Equal("↑", ratio.Marker);
        var context = Row(new() { FriendlyName = "钠", Value = 2500, Unit = "mg",
            ContextReferences = [new(DietaryReferenceIntakeType.PI_NCD, 2000, "mg")] });
        Assert.Equal("—", context.ReferenceRange);
        Assert.Empty(context.Marker);
    }

    [Fact]
    public void Composition_details_join_their_parent_and_water_follows_vitamins()
    {
        var groups = DietaryReportNutrients.Create([
            new() { FriendlyName = "总维生素E", Value = 8, Unit = "mg α-TE" },
            new() { FriendlyName = "总能量", Value = 100, Unit = "kcal" },
            new() { FriendlyName = "铁", Value = 5, Unit = "mg" }
        ], [Total("水分", 80, "g"), Total("γ-生育酚", 2, "mg"), Total("α-生育酚", 3, "mg"),
            Total("总维生素E", 8, "mg α-TE"), Total("钙", 20, "mg"), Total("未知成分", 1, "g")]);
        Assert.Equal(["宏量营养素与能量", "矿物质", "维生素", "水", "其他成分"], groups.Select(group => group.Title));
        Assert.Equal(["钙", "铁"], groups[1].Rows.Select(row => row.Name));
        Assert.Equal(["总维生素E", "α-生育酚", "γ-生育酚"], groups[2].Rows.Select(row => row.Name));
        Assert.False(groups[2].Rows[0].Indented);
        Assert.All(groups[2].Rows.Skip(1), row => { Assert.True(row.Indented); Assert.Equal("—", row.ReferenceRange); });
        Assert.Single(groups[4].Rows);
        var standalone = DietaryReportNutrients.Create([], [Total("α-生育酚", 3, "mg")]);
        Assert.False(Assert.Single(Assert.Single(standalone).Rows).Indented);
    }

    private static DietaryReportNutrientRow Row(DietaryNutrientAssessment assessment) =>
        Assert.Single(Assert.Single(DietaryReportNutrients.Create([assessment], [])).Rows);

    private static NutrientAmount Total(string name, decimal value, string unit) => new()
    {
        Nutrient = new Coding(new Uri("urn:test:nutrient"), name, display: name),
        Amount = new Quantity(value, new Coding(new Uri("urn:test:unit"), unit, display: unit))
    };
}
