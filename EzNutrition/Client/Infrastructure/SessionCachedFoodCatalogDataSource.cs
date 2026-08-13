using EzNutrition.Application.Ports;
using EzNutrition.Shared.Data.Entities;

namespace EzNutrition.Client.Infrastructure;

/// <summary>
/// Reuses reference catalogs for the current browser session while keeping
/// consultation workspaces isolated from one another.
/// </summary>
public sealed class SessionCachedFoodCatalogDataSource(
    IFoodCompositionDataSource inner) : IFoodCompositionDataSource, IDisposable
{
    private readonly CatalogCache<Food> foods = new(Clone);
    private readonly CatalogCache<Nutrient> nutrients = new(Clone);

    public Task<IReadOnlyList<Food>> GetFoodsAsync(
        CancellationToken cancellationToken = default) =>
        foods.GetAsync(inner.GetFoodsAsync, cancellationToken);

    public Task<IReadOnlyList<Nutrient>> GetNutrientsAsync(
        CancellationToken cancellationToken = default) =>
        nutrients.GetAsync(inner.GetNutrientsAsync, cancellationToken);

    public Task<IReadOnlyList<FoodNutrientValue>> GetFoodCompositionAsync(
        string friendlyCode,
        CancellationToken cancellationToken = default) =>
        inner.GetFoodCompositionAsync(friendlyCode, cancellationToken);

    public void Dispose()
    {
        foods.Dispose();
        nutrients.Dispose();
    }

    private static Food Clone(Food source) => new()
    {
        FoodId = source.FoodId,
        FriendlyCode = source.FriendlyCode,
        FriendlyName = source.FriendlyName,
        Cite = source.Cite,
        EdiblePortion = source.EdiblePortion,
        FoodGroups = source.FoodGroups,
        Details = source.Details
    };

    private static Nutrient Clone(Nutrient source) => new()
    {
        NutrientId = source.NutrientId,
        FriendlyName = source.FriendlyName,
        DefaultMeasureUnit = source.DefaultMeasureUnit,
        Details = source.Details
    };

    private sealed class CatalogCache<T>(Func<T, T> clone) : IDisposable
        where T : class
    {
        private readonly SemaphoreSlim gate = new(1, 1);
        private T[]? snapshot;

        public async Task<IReadOnlyList<T>> GetAsync(
            Func<CancellationToken, Task<IReadOnlyList<T>>> load,
            CancellationToken cancellationToken)
        {
            var current = Volatile.Read(ref snapshot);
            if (current is null)
            {
                await gate.WaitAsync(cancellationToken);
                try
                {
                    current = snapshot;
                    if (current is null)
                    {
                        var loaded = await load(cancellationToken);
                        if (loaded.Count == 0)
                        {
                            return [];
                        }

                        current = loaded.Select(clone).ToArray();
                        Volatile.Write(ref snapshot, current);
                    }
                }
                finally
                {
                    gate.Release();
                }
            }

            return current.Select(clone).ToArray();
        }

        public void Dispose() => gate.Dispose();
    }
}
