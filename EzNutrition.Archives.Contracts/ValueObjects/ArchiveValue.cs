using EzNutrition.Archives.Contracts.Identity;

namespace EzNutrition.Archives.Contracts.ValueObjects;

/// <summary>
/// 指定通用档案值的类型。
/// </summary>
public enum ArchiveValueKind
{
    /// <summary>文本。</summary>
    Text = 0,

    /// <summary>布尔值。</summary>
    Boolean = 1,

    /// <summary>整数。</summary>
    Integer = 2,

    /// <summary>十进制数。</summary>
    Decimal = 3,

    /// <summary>带偏移的日期时间。</summary>
    DateTime = 4,

    /// <summary>部分日期。</summary>
    PartialDate = 5,

    /// <summary>编码。</summary>
    Coding = 6,

    /// <summary>数量。</summary>
    Quantity = 7,

    /// <summary>数量范围。</summary>
    QuantityRange = 8,

    /// <summary>逻辑资源引用。</summary>
    LogicalReference = 9,

    /// <summary>确切资源版本引用。</summary>
    VersionedReference = 10
}

/// <summary>
/// 表示扩展和通用评估输入允许承载的封闭值类型基类。
/// </summary>
/// <remarks>
/// 该继承层用于表达有限的判别联合。
/// </remarks>
public abstract class ArchiveValue : IEquatable<ArchiveValue>
{
    private protected ArchiveValue()
    {
    }

    /// <summary>
    /// 获取值类型判别码。
    /// </summary>
    public abstract ArchiveValueKind Kind { get; }

    private protected abstract object EqualityComponent { get; }

    /// <inheritdoc />
    public bool Equals(ArchiveValue? other) =>
        other is not null &&
        GetType() == other.GetType() &&
        Equals(EqualityComponent, other.EqualityComponent);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ArchiveValue);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(GetType(), EqualityComponent);

    /// <summary>
    /// 判断两个档案值是否相等。
    /// </summary>
    public static bool operator ==(ArchiveValue? left, ArchiveValue? right) => Equals(left, right);

    /// <summary>
    /// 判断两个档案值是否不同。
    /// </summary>
    public static bool operator !=(ArchiveValue? left, ArchiveValue? right) => !Equals(left, right);
}

/// <summary>
/// 表示文本值。
/// </summary>
public sealed class TextArchiveValue : ArchiveValue
{
    /// <summary>
    /// 初始化文本值。
    /// </summary>
    /// <param name="value">文本内容。</param>
    public TextArchiveValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }

    /// <summary>
    /// 获取文本内容。
    /// </summary>
    public string Value { get; }

    /// <inheritdoc />
    public override ArchiveValueKind Kind => ArchiveValueKind.Text;

    private protected override object EqualityComponent => Value;
}

/// <summary>
/// 表示布尔值。
/// </summary>
public sealed class BooleanArchiveValue : ArchiveValue
{
    /// <summary>
    /// 初始化布尔值。
    /// </summary>
    /// <param name="value">布尔内容。</param>
    public BooleanArchiveValue(bool value) => Value = value;

    /// <summary>
    /// 获取布尔内容。
    /// </summary>
    public bool Value { get; }

    /// <inheritdoc />
    public override ArchiveValueKind Kind => ArchiveValueKind.Boolean;

    private protected override object EqualityComponent => Value;
}

/// <summary>
/// 表示整数值。
/// </summary>
public sealed class IntegerArchiveValue : ArchiveValue
{
    /// <summary>
    /// 初始化整数值。
    /// </summary>
    /// <param name="value">整数内容。</param>
    public IntegerArchiveValue(long value) => Value = value;

    /// <summary>
    /// 获取整数内容。
    /// </summary>
    public long Value { get; }

    /// <inheritdoc />
    public override ArchiveValueKind Kind => ArchiveValueKind.Integer;

    private protected override object EqualityComponent => Value;
}

/// <summary>
/// 表示无单位的十进制值。
/// </summary>
public sealed class DecimalArchiveValue : ArchiveValue
{
    /// <summary>
    /// 初始化十进制值。
    /// </summary>
    /// <param name="value">十进制内容。</param>
    public DecimalArchiveValue(decimal value) => Value = value;

    /// <summary>
    /// 获取十进制内容。
    /// </summary>
    public decimal Value { get; }

    /// <inheritdoc />
    public override ArchiveValueKind Kind => ArchiveValueKind.Decimal;

    private protected override object EqualityComponent => Value;
}

/// <summary>
/// 表示带偏移的日期时间值。
/// </summary>
public sealed class DateTimeArchiveValue : ArchiveValue
{
    /// <summary>
    /// 初始化日期时间值。
    /// </summary>
    /// <param name="value">带偏移的日期时间。</param>
    public DateTimeArchiveValue(DateTimeOffset value) => Value = value;

    /// <summary>
    /// 获取日期时间内容。
    /// </summary>
    public DateTimeOffset Value { get; }

    /// <inheritdoc />
    public override ArchiveValueKind Kind => ArchiveValueKind.DateTime;

    private protected override object EqualityComponent => Value;
}

/// <summary>
/// 表示部分日期值。
/// </summary>
public sealed class PartialDateArchiveValue : ArchiveValue
{
    /// <summary>
    /// 初始化部分日期值。
    /// </summary>
    /// <param name="value">部分日期。</param>
    public PartialDateArchiveValue(PartialDate value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }

    /// <summary>
    /// 获取部分日期内容。
    /// </summary>
    public PartialDate Value { get; }

    /// <inheritdoc />
    public override ArchiveValueKind Kind => ArchiveValueKind.PartialDate;

    private protected override object EqualityComponent => Value;
}

/// <summary>
/// 表示编码值。
/// </summary>
public sealed class CodingArchiveValue : ArchiveValue
{
    /// <summary>
    /// 初始化编码值。
    /// </summary>
    /// <param name="value">编码内容。</param>
    public CodingArchiveValue(Coding value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }

    /// <summary>
    /// 获取编码内容。
    /// </summary>
    public Coding Value { get; }

    /// <inheritdoc />
    public override ArchiveValueKind Kind => ArchiveValueKind.Coding;

    private protected override object EqualityComponent => Value;
}

/// <summary>
/// 表示数量值。
/// </summary>
public sealed class QuantityArchiveValue : ArchiveValue
{
    /// <summary>
    /// 初始化数量值。
    /// </summary>
    /// <param name="value">数量内容。</param>
    public QuantityArchiveValue(Quantity value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }

    /// <summary>
    /// 获取数量内容。
    /// </summary>
    public Quantity Value { get; }

    /// <inheritdoc />
    public override ArchiveValueKind Kind => ArchiveValueKind.Quantity;

    private protected override object EqualityComponent => Value;
}

/// <summary>
/// 表示数量范围值。
/// </summary>
public sealed class QuantityRangeArchiveValue : ArchiveValue
{
    /// <summary>
    /// 初始化数量范围值。
    /// </summary>
    /// <param name="value">数量范围内容。</param>
    public QuantityRangeArchiveValue(QuantityRange value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }

    /// <summary>
    /// 获取数量范围内容。
    /// </summary>
    public QuantityRange Value { get; }

    /// <inheritdoc />
    public override ArchiveValueKind Kind => ArchiveValueKind.QuantityRange;

    private protected override object EqualityComponent => Value;
}

/// <summary>
/// 表示逻辑资源引用值。
/// </summary>
public sealed class LogicalReferenceArchiveValue : ArchiveValue
{
    /// <summary>
    /// 初始化逻辑资源引用值。
    /// </summary>
    /// <param name="value">逻辑资源引用。</param>
    public LogicalReferenceArchiveValue(LogicalResourceReference value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }

    /// <summary>
    /// 获取逻辑资源引用。
    /// </summary>
    public LogicalResourceReference Value { get; }

    /// <inheritdoc />
    public override ArchiveValueKind Kind => ArchiveValueKind.LogicalReference;

    private protected override object EqualityComponent => Value;
}

/// <summary>
/// 表示确切资源版本引用值。
/// </summary>
public sealed class VersionedReferenceArchiveValue : ArchiveValue
{
    /// <summary>
    /// 初始化确切资源版本引用值。
    /// </summary>
    /// <param name="value">确切资源版本引用。</param>
    public VersionedReferenceArchiveValue(VersionedResourceReference value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }

    /// <summary>
    /// 获取确切资源版本引用。
    /// </summary>
    public VersionedResourceReference Value { get; }

    /// <inheritdoc />
    public override ArchiveValueKind Kind => ArchiveValueKind.VersionedReference;

    private protected override object EqualityComponent => Value;
}
