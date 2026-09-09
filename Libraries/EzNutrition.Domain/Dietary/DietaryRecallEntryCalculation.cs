namespace EzNutrition.Domain.Dietary;

public sealed record DietaryRecallEntryCalculation
{
    /// <summary>获取核算所依据的录入条目标识。</summary>
    public Guid EntryId { get; init; }

    /// <summary>获取核算所依据的食物标识。</summary>
    public Guid FoodId { get; init; }

    public required string FoodName { get; init; }

    public decimal RecordedWeight { get; init; }

    public decimal EdibleWeight { get; init; }

    public MealOccasion MealOccasion { get; init; }

    public bool IsAllEdible { get; init; }

    public required IReadOnlyDictionary<int, decimal> NutrientValues { get; init; }
}
