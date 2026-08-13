namespace EzNutrition.Domain.Dietary;

public sealed record DietaryRecallEntryCalculation
{
    public required string FoodName { get; init; }

    public decimal RecordedWeight { get; init; }

    public bool IsAllEdible { get; init; }

    public required IReadOnlyDictionary<int, decimal> NutrientValues { get; init; }
}
