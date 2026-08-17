using EzNutrition.Server.Data;
using EzNutrition.Server.Data.Repositories;
using EzNutrition.Shared.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EzNutrition.Server.Tests.Repositories;

public sealed class DietaryReferenceIntakeRepositoryTests
{
    [Fact]
    public async Task Decimal_age_selects_the_zero_and_half_year_dri_thresholds()
    {
        await using var context = CreateContext();
        context.DRIs!.AddRange(
            Dri(1, 0m, 100m),
            Dri(2, 0.5m, 200m));
        await context.SaveChangesAsync();
        var repository = new DietaryReferenceIntakeRepository(context);

        var atBirth = await repository.GetDRIsByPersonalInfoAsync(
            0m,
            "女",
            [],
            CancellationToken.None);
        var atSixMonths = await repository.GetDRIsByPersonalInfoAsync(
            0.5m,
            "女",
            [],
            CancellationToken.None);

        Assert.Equal(100m, Assert.Single(atBirth).Value);
        Assert.Equal(200m, Assert.Single(atSixMonths).Value);
    }

    [Fact]
    public async Task Decimal_age_selects_the_half_year_eer_threshold()
    {
        await using var context = CreateContext();
        context.EERs!.AddRange(
            new EER { EERId = 1, Gender = "女", AgeStart = 0m, AvgBwEER = 100 },
            new EER { EERId = 2, Gender = "女", AgeStart = 0.5m, AvgBwEER = 200 });
        await context.SaveChangesAsync();
        var repository = new DietaryReferenceIntakeRepository(context);

        var result = await repository.GetEERsByPersonalInfoAsync(
            0.5m,
            "女",
            [],
            CancellationToken.None);

        Assert.Equal(200, Assert.Single(result).AvgBwEER);
    }

    private static EzNutritionDbContext CreateContext() => new(
        new DbContextOptionsBuilder<EzNutritionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static DietaryReferenceIntakeValue Dri(int id, decimal ageStart, decimal value) => new()
    {
        DietaryReferenceIntakeValueId = id,
        AgeStart = ageStart,
        Gender = "女",
        Nutrient = "合成营养素",
        RecordType = DietaryReferenceIntakeType.RNI,
        Value = value,
        MeasureUnit = "mg/d"
    };
}
