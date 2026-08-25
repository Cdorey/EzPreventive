namespace EzNutrition.Shared.Data.DTO.PromptDto;

/// <summary>
/// 表示披露给 AI 建议接口的实足年龄，并保留年、月、日的已知精度。
/// </summary>
/// <remarks>
/// 月和日使用可空值区分“不知道”与“明确为零”。该传输值不包含用于参考数据匹配的十进制年龄投影。
/// </remarks>
public sealed record PatientAge
{
    /// <summary>
    /// 初始化实足年龄。
    /// </summary>
    /// <param name="years">完整年数。</param>
    /// <param name="months">年后完整月数；为空表示未记录月精度。</param>
    /// <param name="days">月后完整日数；为空表示未记录日精度。</param>
    public PatientAge(int years, int? months = null, int? days = null)
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
}
