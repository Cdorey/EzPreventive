namespace EzNutrition.Archives.Contracts.ValueObjects;

/// <summary>
/// 指定数量值与边界的比较关系。
/// </summary>
public enum QuantityComparator
{
    /// <summary>
    /// 数值本身即为精确或常规表示值。
    /// </summary>
    None = 0,

    /// <summary>
    /// 实际值小于所记录数值。
    /// </summary>
    LessThan = 1,

    /// <summary>
    /// 实际值小于或等于所记录数值。
    /// </summary>
    LessThanOrEqual = 2,

    /// <summary>
    /// 实际值大于或等于所记录数值。
    /// </summary>
    GreaterThanOrEqual = 3,

    /// <summary>
    /// 实际值大于所记录数值。
    /// </summary>
    GreaterThan = 4
}

/// <summary>
/// 表示具有稳定单位代码的十进制数量。
/// </summary>
public sealed record Quantity
{
    /// <summary>
    /// 初始化数量。
    /// </summary>
    /// <param name="value">十进制数值。</param>
    /// <param name="unit">单位编码，优先使用 UCUM。</param>
    /// <param name="comparator">可选的边界比较关系。</param>
    public Quantity(decimal value, Coding unit, QuantityComparator comparator = QuantityComparator.None)
    {
        ArgumentNullException.ThrowIfNull(unit);
        Value = value;
        Unit = unit;
        Comparator = comparator;
    }

    /// <summary>
    /// 获取十进制数值。
    /// </summary>
    public decimal Value { get; }

    /// <summary>
    /// 获取单位编码。
    /// </summary>
    public Coding Unit { get; }

    /// <summary>
    /// 获取边界比较关系。
    /// </summary>
    public QuantityComparator Comparator { get; }
}

/// <summary>
/// 表示具有相同单位的数量范围。
/// </summary>
public sealed record QuantityRange
{
    /// <summary>
    /// 初始化数量范围。
    /// </summary>
    /// <param name="low">可选下界。</param>
    /// <param name="high">可选上界。</param>
    /// <exception cref="ArgumentException">边界缺失、单位不同、比较符方向错误或区间为空。</exception>
    public QuantityRange(Quantity? low, Quantity? high)
    {
        if (low is null && high is null)
        {
            throw new ArgumentException("数量范围至少需要一个边界。", nameof(low));
        }

        if (low is not null && high is not null)
        {
            if (!low.Unit.HasSameIdentity(high.Unit))
            {
                throw new ArgumentException("数量范围的上下界必须使用相同单位。", nameof(high));
            }

            if (low.Value > high.Value ||
                (low.Value == high.Value &&
                 (low.Comparator == QuantityComparator.GreaterThan ||
                  high.Comparator == QuantityComparator.LessThan)))
            {
                throw new ArgumentException("数量范围的上下界形成了空区间。", nameof(low));
            }
        }

        if (low?.Comparator is QuantityComparator.LessThan or QuantityComparator.LessThanOrEqual)
        {
            throw new ArgumentException("数量范围下界只能使用大于方向的比较符。", nameof(low));
        }

        if (high?.Comparator is QuantityComparator.GreaterThan or QuantityComparator.GreaterThanOrEqual)
        {
            throw new ArgumentException("数量范围上界只能使用小于方向的比较符。", nameof(high));
        }

        Low = low;
        High = high;
    }

    /// <summary>
    /// 获取可选下界。
    /// </summary>
    public Quantity? Low { get; }

    /// <summary>
    /// 获取可选上界。
    /// </summary>
    public Quantity? High { get; }
}
