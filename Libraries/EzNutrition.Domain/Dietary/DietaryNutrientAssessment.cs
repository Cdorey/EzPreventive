using EzNutrition.Shared.Data.Entities;

namespace EzNutrition.Domain.Dietary;

public enum DietaryReferenceStatus
{
    NotEstablished,
    WithinRange,
    BelowRange,
    AboveRange
}

public sealed record DietaryNutrientAssessment
{
    public string Abbreviation { get; init; } = string.Empty;

    public required string FriendlyName { get; init; }

    public decimal Value { get; init; }

    public string Unit { get; init; } = string.Empty;

    public DietaryNutrientReference? LowerReference { get; init; }

    public DietaryNutrientReference? UpperReference { get; init; }

    public IReadOnlyList<DietaryNutrientReference> ContextReferences { get; init; } = [];

    public IReadOnlyList<DietaryMealEnergy> MealEnergies { get; init; } = [];

    public IReadOnlyList<DietaryFoodContribution> FoodContributions { get; init; } = [];

    public bool HasReference => LowerReference is not null || UpperReference is not null;

    public DietaryReferenceStatus ReferenceStatus
    {
        get
        {
            if (LowerReference is { Value: var lower } && Value < lower)
            {
                return DietaryReferenceStatus.BelowRange;
            }

            if (UpperReference is { Value: var upper } && Value > upper)
            {
                return DietaryReferenceStatus.AboveRange;
            }

            return HasReference
                ? DietaryReferenceStatus.WithinRange
                : DietaryReferenceStatus.NotEstablished;
        }
    }
}

public sealed record DietaryNutrientReference(
    DietaryReferenceIntakeType Type,
    decimal Value,
    string Unit);

public sealed record DietaryMealEnergy(
    MealOccasion MealOccasion,
    decimal Energy,
    decimal PercentageOfTotalEnergy);

public sealed record DietaryFoodContribution(
    string FoodName,
    decimal Value,
    string Unit);
