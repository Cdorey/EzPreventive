using System.Globalization;

namespace EzNutrition.UI.Services;

/// <summary>
/// 使用宿主选择的显示时区格式化绝对时间。
/// </summary>
public interface ILocalDateTimeFormatter
{
    /// <summary>
    /// 转换并格式化一个用于显示的绝对时间。
    /// </summary>
    string Format(DateTimeOffset value, string format);
}

/// <summary>
/// 使用宿主提供的固定时区格式化时间。
/// </summary>
public sealed class LocalDateTimeFormatter : ILocalDateTimeFormatter
{
    private readonly TimeZoneInfo timeZone;

    /// <summary>
    /// 为 <paramref name="timeZone"/> 创建格式化器。
    /// </summary>
    public LocalDateTimeFormatter(TimeZoneInfo timeZone)
    {
        this.timeZone = timeZone ?? throw new ArgumentNullException(nameof(timeZone));
    }

    /// <inheritdoc />
    public string Format(DateTimeOffset value, string format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(format);
        return TimeZoneInfo.ConvertTime(value, timeZone).ToString(format, CultureInfo.CurrentCulture);
    }
}
