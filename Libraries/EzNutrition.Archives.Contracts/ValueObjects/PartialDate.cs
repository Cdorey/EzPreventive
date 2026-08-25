using System.Globalization;

namespace EzNutrition.Archives.Contracts.ValueObjects;

/// <summary>
/// 表示部分日期的精度。
/// </summary>
public enum PartialDatePrecision
{
    /// <summary>
    /// 仅知道年份。
    /// </summary>
    Year = 1,

    /// <summary>
    /// 知道年份和月份。
    /// </summary>
    Month = 2,

    /// <summary>
    /// 知道完整日期。
    /// </summary>
    Day = 3
}

/// <summary>
/// 表示不使用虚构日期补齐未知部分的年、年月或完整日期。
/// </summary>
public sealed record PartialDate
{
    /// <summary>
    /// 初始化部分日期。
    /// </summary>
    /// <param name="year">年份。</param>
    /// <param name="month">可选月份。</param>
    /// <param name="day">可选日期；提供日期时必须同时提供月份。</param>
    public PartialDate(int year, int? month = null, int? day = null)
    {
        if (year is < 1 or > 9999)
        {
            throw new ArgumentOutOfRangeException(nameof(year), year, "年份必须位于 1 至 9999 之间。");
        }

        if (day is not null && month is null)
        {
            throw new ArgumentException("提供日期时必须同时提供月份。", nameof(day));
        }

        if (month is not null)
        {
            _ = new DateOnly(year, month.Value, day ?? 1);
        }

        Year = year;
        Month = month;
        Day = day;
        Precision = day is not null
            ? PartialDatePrecision.Day
            : month is not null
                ? PartialDatePrecision.Month
                : PartialDatePrecision.Year;
    }

    /// <summary>
    /// 获取年份。
    /// </summary>
    public int Year { get; }

    /// <summary>
    /// 获取月份。
    /// </summary>
    public int? Month { get; }

    /// <summary>
    /// 获取日期。
    /// </summary>
    public int? Day { get; }

    /// <summary>
    /// 获取日期精度。
    /// </summary>
    public PartialDatePrecision Precision { get; }

    /// <inheritdoc />
    public override string ToString() => Precision switch
    {
        PartialDatePrecision.Year => Year.ToString("D4", CultureInfo.InvariantCulture),
        PartialDatePrecision.Month => FormattableString.Invariant($"{Year:D4}-{Month!.Value:D2}"),
        PartialDatePrecision.Day => FormattableString.Invariant($"{Year:D4}-{Month!.Value:D2}-{Day!.Value:D2}"),
        _ => throw new InvalidOperationException("无法识别的部分日期精度。")
    };
}
