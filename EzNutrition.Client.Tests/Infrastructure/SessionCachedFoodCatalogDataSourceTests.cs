using EzNutrition.Application.Ports;
using EzNutrition.Client.Infrastructure;
using EzNutrition.Shared.Data.Entities;

namespace EzNutrition.Client.Tests.Infrastructure;

public sealed class SessionCachedFoodCatalogDataSourceTests
{
    [Fact]
    public async Task ReusesCatalogDownloadsButReturnsIsolatedWorkspaceObjects()
    {
        var inner = new StubFoodCompositionDataSource();
        using var cache = new SessionCachedFoodCatalogDataSource(inner);

        var firstFoods = await cache.GetFoodsAsync();
        var firstNutrients = await cache.GetNutrientsAsync();
        firstFoods[0].FriendlyName = "changed";
        firstFoods[0].FoodNutrientValues = [new FoodNutrientValue { Value = 1m }];
        firstNutrients[0].FriendlyName = "changed";

        var secondFoods = await cache.GetFoodsAsync();
        var secondNutrients = await cache.GetNutrientsAsync();

        Assert.Equal(1, inner.FoodsRequestCount);
        Assert.Equal(1, inner.NutrientsRequestCount);
        Assert.NotSame(firstFoods[0], secondFoods[0]);
        Assert.NotSame(firstNutrients[0], secondNutrients[0]);
        Assert.Equal("猪里脊", secondFoods[0].FriendlyName);
        Assert.Null(secondFoods[0].FoodNutrientValues);
        Assert.Equal("能量", secondNutrients[0].FriendlyName);
    }

    [Fact]
    public async Task CoalescesConcurrentCatalogDownloads()
    {
        var inner = new StubFoodCompositionDataSource
        {
            FoodsRelease = new(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        using var cache = new SessionCachedFoodCatalogDataSource(inner);

        var first = cache.GetFoodsAsync();
        await inner.FoodsRequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        var second = cache.GetFoodsAsync();
        inner.FoodsRelease.SetResult(true);

        var results = await Task.WhenAll(first, second);

        Assert.Equal(1, inner.FoodsRequestCount);
        Assert.NotSame(results[0][0], results[1][0]);
    }

    [Fact]
    public async Task RetriesCatalogDownloadAfterFailure()
    {
        var inner = new StubFoodCompositionDataSource
        {
            FoodsException = new NutritionDataAccessException("temporary failure")
        };
        using var cache = new SessionCachedFoodCatalogDataSource(inner);

        await Assert.ThrowsAsync<NutritionDataAccessException>(() => cache.GetFoodsAsync());
        inner.FoodsException = null;

        var foods = await cache.GetFoodsAsync();

        Assert.Single(foods);
        Assert.Equal(2, inner.FoodsRequestCount);
    }

    [Fact]
    public async Task KeepsFoodCompositionLoadingOnDemand()
    {
        var inner = new StubFoodCompositionDataSource();
        using var cache = new SessionCachedFoodCatalogDataSource(inner);

        await cache.GetFoodCompositionAsync("PORK-001");
        await cache.GetFoodCompositionAsync("PORK-001");

        Assert.Equal(2, inner.CompositionRequestCount);
    }

    private sealed class StubFoodCompositionDataSource : IFoodCompositionDataSource
    {
        public int FoodsRequestCount { get; private set; }

        public int NutrientsRequestCount { get; private set; }

        public int CompositionRequestCount { get; private set; }

        public Exception? FoodsException { get; set; }

        public TaskCompletionSource<bool> FoodsRequestStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool>? FoodsRelease { get; init; }

        public async Task<IReadOnlyList<Food>> GetFoodsAsync(
            CancellationToken cancellationToken = default)
        {
            FoodsRequestCount++;
            FoodsRequestStarted.TrySetResult(true);
            if (FoodsRelease is not null)
            {
                await FoodsRelease.Task.WaitAsync(cancellationToken);
            }

            if (FoodsException is not null)
            {
                throw FoodsException;
            }

            return
            [
                new Food
                {
                    FoodId = Guid.NewGuid(),
                    FriendlyCode = "PORK-001",
                    FriendlyName = "猪里脊",
                    FoodGroups = "畜肉类"
                }
            ];
        }

        public Task<IReadOnlyList<Nutrient>> GetNutrientsAsync(
            CancellationToken cancellationToken = default)
        {
            NutrientsRequestCount++;
            return Task.FromResult<IReadOnlyList<Nutrient>>(
            [
                new Nutrient
                {
                    NutrientId = 1,
                    FriendlyName = "能量",
                    DefaultMeasureUnit = "kCal"
                }
            ]);
        }

        public Task<IReadOnlyList<FoodNutrientValue>> GetFoodCompositionAsync(
            string friendlyCode,
            CancellationToken cancellationToken = default)
        {
            CompositionRequestCount++;
            return Task.FromResult<IReadOnlyList<FoodNutrientValue>>([]);
        }
    }
}
