using EzNutrition.Shared.Data.Entities;
using EzNutrition.UI.Components;

namespace EzNutrition.Client.Tests.Components;

public sealed class FoodSearchSelectTests
{
    [Theory]
    [InlineData("猪", "猪里脊")]
    [InlineData(" pork-001 ", "猪里脊")]
    [InlineData("畜肉类", "猪里脊")]
    public void FindsFoodsBySupportedCatalogFields(string query, string expectedName)
    {
        var foods = new[]
        {
            Food("猪里脊", "PORK-001", "畜肉类"),
            Food("白菜", "VEG-001", "蔬菜类")
        };

        var result = FoodSearchSelect.FindCandidates(foods, query);

        var match = Assert.Single(result.Items);
        Assert.Equal(expectedName, match.FriendlyName);
        Assert.False(result.HasMore);
    }

    [Fact]
    public void OrdersMatchesByRelevanceAndPreservesCatalogOrderForTies()
    {
        var foods = new[]
        {
            Food("里脊", "MEAT-01", "猪肉类"),
            Food("五花肉", "X-猪-01", "畜肉类"),
            Food("黑猪肉", "MEAT-02", "畜肉类"),
            Food("土猪肉", "MEAT-03", "畜肉类"),
            Food("猪排", "MEAT-04", "畜肉类"),
            Food("肉排", "猪排-01", "畜肉类"),
            Food("肉类", "猪", "畜肉类"),
            Food("猪", "MEAT-05", "畜肉类")
        };

        var result = FoodSearchSelect.FindCandidates(foods, "猪");

        Assert.Equal(
            ["猪", "肉类", "猪排", "肉排", "黑猪肉", "土猪肉", "五花肉", "里脊"],
            result.Items.Select(food => food.FriendlyName));
    }

    [Theory]
    [InlineData(50, false)]
    [InlineData(51, true)]
    public void CapsCandidateCountAndReportsAdditionalMatches(int count, bool expectedHasMore)
    {
        var foods = Enumerable.Range(0, count)
            .Select(index => Food($"猪肉 {index:00}", $"PORK-{index:00}", "畜肉类"))
            .Append(Food("白菜", "VEG-001", "蔬菜类"));

        var result = FoodSearchSelect.FindCandidates(foods, "猪");

        Assert.Equal(Math.Min(count, 50), result.Items.Length);
        Assert.Equal(expectedHasMore, result.HasMore);
        Assert.Equal(
            Enumerable.Range(0, Math.Min(count, 50)).Select(index => $"PORK-{index:00}"),
            result.Items.Select(food => food.FriendlyCode));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RequiresAQueryBeforeReturningCandidates(string? query)
    {
        var result = FoodSearchSelect.FindCandidates(
            [Food("猪里脊", "PORK-001", "畜肉类")],
            query);

        Assert.Empty(result.Items);
        Assert.False(result.HasMore);
    }

    [Fact]
    public void ReturnsAnEmptyResultWhenNothingMatches()
    {
        var result = FoodSearchSelect.FindCandidates(
            [new Food { FoodId = Guid.NewGuid() }],
            "猪");

        Assert.Empty(result.Items);
        Assert.False(result.HasMore);
    }

    private static Food Food(string name, string code, string group) => new()
    {
        FoodId = Guid.NewGuid(),
        FriendlyName = name,
        FriendlyCode = code,
        FoodGroups = group
    };
}
