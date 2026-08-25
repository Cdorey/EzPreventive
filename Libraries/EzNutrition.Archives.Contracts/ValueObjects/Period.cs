namespace EzNutrition.Archives.Contracts.ValueObjects;

/// <summary>
/// 表示具有必填开始时间和可选结束时间的时间段。
/// </summary>
public sealed record Period
{
    /// <summary>
    /// 初始化时间段。
    /// </summary>
    /// <param name="start">开始时间。</param>
    /// <param name="end">可选结束时间。</param>
    /// <exception cref="ArgumentException"><paramref name="end"/> 早于 <paramref name="start"/>。</exception>
    public Period(DateTimeOffset start, DateTimeOffset? end = null)
    {
        if (end < start)
        {
            throw new ArgumentException("时间段结束时间不能早于开始时间。", nameof(end));
        }

        Start = start;
        End = end;
    }

    /// <summary>
    /// 获取开始时间。
    /// </summary>
    public DateTimeOffset Start { get; }

    /// <summary>
    /// 获取可选结束时间。
    /// </summary>
    public DateTimeOffset? End { get; }
}

/// <summary>
/// 表示数据缺失的明确原因。
/// </summary>
public enum DataAbsentReasonCode
{
    /// <summary>
    /// 原因未知。
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// 未向咨询对象询问。
    /// </summary>
    NotAsked = 1,

    /// <summary>
    /// 在当前情境下不适用。
    /// </summary>
    NotApplicable = 2,

    /// <summary>
    /// 信息被有意隐去。
    /// </summary>
    Withheld = 3,

    /// <summary>
    /// 尚未建立或无法获得该值。
    /// </summary>
    NotEstablished = 4,

    /// <summary>
    /// 当前程序或格式不支持表达该值。
    /// </summary>
    Unsupported = 5
}
