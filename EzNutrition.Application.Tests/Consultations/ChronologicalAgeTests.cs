using EzNutrition.Domain.Consultations;

namespace EzNutrition.Application.Tests.Consultations;

/// <summary>
/// 验证实足年龄的日历计算和参考数据投影。
/// </summary>
public sealed class ChronologicalAgeTests
{
    [Fact]
    public void Birth_date_produces_calendar_year_month_day_age()
    {
        var age = ChronologicalAge.FromBirthDate(
            new DateOnly(2024, 4, 17),
            new DateOnly(2025, 9, 9));

        Assert.Equal(1, age.Years);
        Assert.Equal(4, age.Months);
        Assert.Equal(23, age.Days);
        Assert.Equal("1岁4个月23天", age.ToString());
    }

    [Fact]
    public void Six_month_age_maps_to_existing_half_year_reference_threshold()
    {
        var age = new ChronologicalAge(0, 6, 0);

        Assert.Equal(0.5m, age.ToReferenceYears());
    }

    [Fact]
    public void Reported_whole_years_do_not_fabricate_month_or_day_precision()
    {
        var age = new ChronologicalAge(25);

        Assert.Null(age.Months);
        Assert.Null(age.Days);
        Assert.Equal(25m, age.ToReferenceYears());
        Assert.Equal("25岁", age.ToString());
    }

    [Fact]
    public void Future_birth_date_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ChronologicalAge.FromBirthDate(
            new DateOnly(2026, 8, 18),
            new DateOnly(2026, 8, 17)));
    }
}
