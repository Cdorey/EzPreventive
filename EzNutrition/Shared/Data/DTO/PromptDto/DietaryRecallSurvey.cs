using System.Text.Json.Serialization;

namespace EzNutrition.Shared.Data.DTO.PromptDto;

/// <summary>
/// Carries the compact, already calculated projection of a dietary recall.
/// </summary>
public sealed class DietaryRecallSurvey
{
    public string Method { get; init; } = "24-hour-recall";

    public int RecallDays { get; init; } = 1;

    public DietaryRecallFoodItem[] Foods { get; init; } = [];

    public DietaryNutrientIntake[] Nutrients { get; init; } = [];
}

public sealed record DietaryRecallFoodItem(
    string FoodName,
    DietaryMealOccasion Meal,
    decimal EdibleAmount,
    string Unit);

public sealed record DietaryNutrientIntake(
    string Name,
    decimal Intake,
    string Unit,
    DietaryReferenceComparison ReferenceComparison,
    DietaryReferenceTarget[] References,
    DietaryMealEnergyShare[]? MealEnergyShares = null,
    DietaryFoodSource[]? TopFoodSources = null);

public sealed record DietaryReferenceTarget(
    string Type,
    decimal Value,
    string Unit);

public sealed record DietaryMealEnergyShare(
    DietaryMealOccasion Meal,
    decimal Energy,
    decimal PercentageOfTotalEnergy);

public sealed record DietaryFoodSource(
    string FoodName,
    decimal Amount,
    string Unit);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DietaryReferenceComparison
{
    NotEstablished,
    WithinReference,
    BelowReference,
    AboveReference
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DietaryMealOccasion
{
    Breakfast,
    MorningSnack,
    Lunch,
    AfternoonSnack,
    Dinner,
    LateNightSnack
}
