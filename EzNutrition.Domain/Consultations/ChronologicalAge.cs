using System.Globalization;

namespace EzNutrition.Domain.Consultations;

/// <summary>
/// 表示咨询时采用的实足年龄，并保留年、月、日的已知精度。
/// </summary>
/// <remarks>
/// 月和日使用可空值区分“不知道”与“明确为零”。该值不是固定天数时长；
/// 由生日推导时必须同时提供计算年龄所依据的日期。
/// </remarks>
public sealed record ChronologicalAge
{
    private const decimal AverageDaysPerYear = 365.2425m;

    /// <summary>
    /// 初始化实足年龄。
    /// </summary>
    /// <param name="years">完整年数。</param>
    /// <param name="months">年后完整月数；为空表示未记录月精度。</param>
    /// <param name="days">月后完整日数；为空表示未记录日精度。</param>
    public ChronologicalAge(int years, int? months = null, int? days = null)
    {
        if (years < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(years), years, "年龄年数不能为负数。");
        }

        if (months is < 0 or > 11)
        {
            throw new ArgumentOutOfRangeException(nameof(months), months, "年龄月数必须位于 0 至 11 之间。");
        }

        if (days is not null && months is null)
        {
            throw new ArgumentException("记录年龄日数时必须同时记录月数。", nameof(days));
        }

        if (days is < 0 or > 30)
        {
            throw new ArgumentOutOfRangeException(nameof(days), days, "年龄日数必须位于 0 至 30 之间。");
        }

        Years = years;
        Months = months;
        Days = days;
    }

    /// <summary>获取完整年数。</summary>
    public int Years { get; }

    /// <summary>获取年后的完整月数；为空表示未记录月精度。</summary>
    public int? Months { get; }

    /// <summary>获取月后的完整日数；为空表示未记录日精度。</summary>
    public int? Days { get; }

    /// <summary>
    /// 计算供现有营养参考数据年龄阈值使用的十进制年。
    /// </summary>
    /// <remarks>
    /// 该投影只用于与以十进制年保存的参考数据比较，不能用于重建原始复合年龄。
    /// </remarks>
    public decimal ToReferenceYears() =>
        Years + ((Months ?? 0) / 12m) + ((Days ?? 0) / AverageDaysPerYear);

    /// <summary>
    /// 根据出生日期和计算日期生成实足年龄。
    /// </summary>
    public static ChronologicalAge FromBirthDate(DateOnly birthDate, DateOnly asOfDate)
    {
        if (birthDate > asOfDate)
        {
            throw new ArgumentOutOfRangeException(nameof(birthDate), birthDate, "出生日期不能晚于年龄计算日期。");
        }

        var years = asOfDate.Year - birthDate.Year;
        var yearAnchor = birthDate.AddYears(years);
        if (yearAnchor > asOfDate)
        {
            years--;
            yearAnchor = birthDate.AddYears(years);
        }

        var months = ((asOfDate.Year - yearAnchor.Year) * 12) + asOfDate.Month - yearAnchor.Month;
        var monthAnchor = yearAnchor.AddMonths(months);
        if (monthAnchor > asOfDate)
        {
            months--;
            monthAnchor = yearAnchor.AddMonths(months);
        }

        var days = asOfDate.DayNumber - monthAnchor.DayNumber;
        return new ChronologicalAge(years, months, days);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var text = Years.ToString(CultureInfo.InvariantCulture) + "岁";
        if (Months is { } months)
        {
            text += months.ToString(CultureInfo.InvariantCulture) + "个月";
        }

        if (Days is { } days)
        {
            text += days.ToString(CultureInfo.InvariantCulture) + "天";
        }

        return text;
    }
}
