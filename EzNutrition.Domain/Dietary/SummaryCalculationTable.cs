using EzNutrition.Shared.Data.Entities;

namespace EzNutrition.Domain.Dietary;

public sealed class SummaryCalculationTable
{
    private readonly Dictionary<int, Nutrient> nutrientsById = [];
    private readonly Dictionary<string, Nutrient> nutrientsByName = new(StringComparer.Ordinal);
    private readonly Dictionary<int, decimal> totalsByNutrientId = [];
    private readonly Dictionary<Guid, NutrientValueAccumulator> totalsByFoodId = [];
    private readonly Dictionary<MealOccasion, NutrientValueAccumulator> totalsByMeal = [];
    private readonly IReadOnlyList<Food> foodsInSurveyOrder;
    private readonly IReadOnlyList<DietaryRecallEntryCalculation> entryCalculations;

    public SummaryCalculationTable(
        List<DietaryRecallEntry> dietaryRecallEntries,
        List<Nutrient> nutrients)
    {
        IndexNutrients(nutrients);

        var foods = new List<Food>();
        var seenFoods = new HashSet<Food>();
        var calculations = new List<DietaryRecallEntryCalculation>(dietaryRecallEntries.Count);

        foreach (var entry in dietaryRecallEntries)
        {
            if (seenFoods.Add(entry.Food))
            {
                foods.Add(entry.Food);
            }

            var entryTotals = AccumulateEntry(entry);
            calculations.Add(new DietaryRecallEntryCalculation
            {
                FoodName = entry.Food.FriendlyName ?? string.Empty,
                RecordedWeight = entry.Weight,
                IsAllEdible = entry.IsAllEdible,
                NutrientValues = nutrients.ToDictionary(
                    nutrient => nutrient.NutrientId,
                    nutrient => entryTotals.GetValueOrDefault(nutrient.NutrientId))
            });
        }

        foodsInSurveyOrder = foods.ToArray();
        entryCalculations = calculations.ToArray();
    }

    public decimal this[Nutrient nutrient] =>
        totalsByNutrientId.GetValueOrDefault(nutrient.NutrientId);

    public decimal this[string nutrientFriendlyName] => this[FindNutrient(nutrientFriendlyName)];

    public IEnumerable<FoodNutrientValue> this[Food food] =>
        totalsByFoodId.TryGetValue(food.FoodId, out var totals)
            ? totals.CreateValues(food)
            : [];

    public IEnumerable<FoodNutrientValue> this[MealOccasion mealOccasion] =>
        totalsByMeal.TryGetValue(mealOccasion, out var totals)
            ? totals.CreateValues()
            : [];

    public decimal TotalEnergy => this["能量"];

    public decimal CarbohydrateEnergy => this["碳水化合物"] * 4;

    public decimal FatEnergy => this["脂肪"] * 9;

    public decimal ProteinEnergy => this["蛋白质"] * 4;

    public IEnumerable<FoodNutrientValue> CarbohydrateRank => CreateRank("碳水化合物");

    public IEnumerable<FoodNutrientValue> FatRank => CreateRank("脂肪");

    public IEnumerable<FoodNutrientValue> ProteinRank => CreateRank("蛋白质");

    public IReadOnlyList<DietaryRecallEntryCalculation> CreateEntryCalculations() =>
        entryCalculations;

    internal decimal GetValue(MealOccasion mealOccasion, string nutrientFriendlyName)
    {
        if (!totalsByMeal.TryGetValue(mealOccasion, out var totals))
        {
            return 0m;
        }

        return totals.GetValue(FindNutrient(nutrientFriendlyName).NutrientId);
    }

    private Dictionary<int, decimal> AccumulateEntry(DietaryRecallEntry entry)
    {
        var effectiveWeight = entry.IsAllEdible
            ? entry.Weight
            : entry.Weight * (entry.Food.EdiblePortion ?? 100) / 100;
        var entryTotals = new Dictionary<int, decimal>();
        var foodTotals = GetOrAdd(totalsByFoodId, entry.Food.FoodId);
        var mealTotals = GetOrAdd(totalsByMeal, entry.MealOccasion);

        foreach (var sourceValue in entry.Food.FoodNutrientValues!)
        {
            var nutrient = FindNutrient(sourceValue.NutrientId);
            var value = sourceValue.Value * effectiveWeight / 100;

            AddValue(entryTotals, nutrient.NutrientId, value);
            AddValue(totalsByNutrientId, sourceValue.Nutrient!.NutrientId, value);
            foodTotals.Add(nutrient, sourceValue.MeasureUnit, value);
            mealTotals.Add(nutrient, sourceValue.MeasureUnit, value);
        }

        return entryTotals;
    }

    private IEnumerable<FoodNutrientValue> CreateRank(string nutrientFriendlyName)
    {
        return foodsInSurveyOrder
            .SelectMany(food => totalsByFoodId[food.FoodId].CreateValues(food, nutrientFriendlyName))
            .OrderByDescending(value => value.Value);
    }

    private void IndexNutrients(IEnumerable<Nutrient> source)
    {
        foreach (var nutrient in source)
        {
            nutrientsById.TryAdd(nutrient.NutrientId, nutrient);
            if (nutrient.FriendlyName is not null)
            {
                nutrientsByName.TryAdd(nutrient.FriendlyName, nutrient);
            }
        }
    }

    private Nutrient FindNutrient(int nutrientId) =>
        nutrientsById.TryGetValue(nutrientId, out var nutrient)
            ? nutrient
            : throw new InvalidOperationException($"Nutrient {nutrientId} was not found.");

    private Nutrient FindNutrient(string friendlyName) =>
        nutrientsByName.TryGetValue(friendlyName, out var nutrient)
            ? nutrient
            : throw new InvalidOperationException($"Nutrient '{friendlyName}' was not found.");

    private static NutrientValueAccumulator GetOrAdd<TKey>(
        IDictionary<TKey, NutrientValueAccumulator> source,
        TKey key)
        where TKey : notnull
    {
        if (source.TryGetValue(key, out var totals))
        {
            return totals;
        }

        totals = new NutrientValueAccumulator();
        source.Add(key, totals);
        return totals;
    }

    private static void AddValue(IDictionary<int, decimal> source, int nutrientId, decimal value)
    {
        source.TryGetValue(nutrientId, out var currentValue);
        source[nutrientId] = currentValue + value;
    }

    private sealed class NutrientValueAccumulator
    {
        private readonly Dictionary<int, AggregatedNutrientValue> valuesByNutrientId = [];
        private readonly List<AggregatedNutrientValue> valuesInSourceOrder = [];

        public void Add(Nutrient nutrient, string? measureUnit, decimal value)
        {
            if (valuesByNutrientId.TryGetValue(nutrient.NutrientId, out var aggregate))
            {
                aggregate.Value += value;
                return;
            }

            aggregate = new AggregatedNutrientValue(
                nutrient,
                measureUnit ?? nutrient.DefaultMeasureUnit,
                value);
            valuesByNutrientId.Add(nutrient.NutrientId, aggregate);
            valuesInSourceOrder.Add(aggregate);
        }

        public decimal GetValue(int nutrientId) =>
            valuesByNutrientId.TryGetValue(nutrientId, out var value)
                ? value.Value
                : 0m;

        public IReadOnlyList<FoodNutrientValue> CreateValues(
            Food? food = null,
            string? nutrientFriendlyName = null)
        {
            return valuesInSourceOrder
                .Where(value => nutrientFriendlyName is null
                    || value.Nutrient.FriendlyName == nutrientFriendlyName)
                .Select(value => new FoodNutrientValue
                {
                    Food = food,
                    FoodId = food?.FoodId ?? Guid.Empty,
                    Nutrient = value.Nutrient,
                    NutrientId = value.Nutrient.NutrientId,
                    MeasureUnit = value.MeasureUnit,
                    Value = value.Value
                })
                .ToArray();
        }
    }

    private sealed class AggregatedNutrientValue(
        Nutrient nutrient,
        string? measureUnit,
        decimal value)
    {
        public Nutrient Nutrient { get; } = nutrient;

        public string? MeasureUnit { get; } = measureUnit;

        public decimal Value { get; set; } = value;
    }
}
