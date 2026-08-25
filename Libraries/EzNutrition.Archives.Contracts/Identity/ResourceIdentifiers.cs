namespace EzNutrition.Archives.Contracts.Identity;

/// <summary>
/// 表示跨版本保持不变的逻辑资源标识。
/// </summary>
public sealed record ResourceId
{
    /// <summary>
    /// 初始化逻辑资源标识。
    /// </summary>
    /// <param name="value">非空 UUID。</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> 为空 UUID。</exception>
    public ResourceId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("逻辑资源标识不能是空 UUID。", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// 获取 UUID 值。
    /// </summary>
    public Guid Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString("D");
}

/// <summary>
/// 表示某个逻辑资源的一次确切版本标识。
/// </summary>
public sealed record ResourceVersionId
{
    /// <summary>
    /// 初始化资源版本标识。
    /// </summary>
    /// <param name="value">非空 UUID。</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> 为空 UUID。</exception>
    public ResourceVersionId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("资源版本标识不能是空 UUID。", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// 获取 UUID 值。
    /// </summary>
    public Guid Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString("D");
}

/// <summary>
/// 表示供人阅读和一致性检查使用的资源修订序号。
/// </summary>
/// <remarks>
/// 修订先后关系以 <c>Supersedes</c> 为准，不得仅凭此序号自动解决分支冲突。
/// </remarks>
public sealed record RevisionNumber
{
    /// <summary>
    /// 初始化修订序号。
    /// </summary>
    /// <param name="value">从 1 开始的正整数。</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> 小于 1。</exception>
    public RevisionNumber(int value)
    {
        if (value < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "修订序号必须从 1 开始。");
        }

        Value = value;
    }

    /// <summary>
    /// 获取修订序号。
    /// </summary>
    public int Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// 表示仅在所属资源内部有效的稳定条目标识。
/// </summary>
public sealed record LocalIdentifier
{
    /// <summary>
    /// 初始化局部标识。
    /// </summary>
    /// <param name="value">在所属资源内唯一的非空字符串。</param>
    public LocalIdentifier(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    /// <summary>
    /// 获取标识文本。
    /// </summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>
/// 表示档案 Bundle 的全局标识。
/// </summary>
public sealed record ArchiveBundleId
{
    /// <summary>
    /// 初始化 Bundle 标识。
    /// </summary>
    /// <param name="value">非空 UUID。</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> 为空 UUID。</exception>
    public ArchiveBundleId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Bundle 标识不能是空 UUID。", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// 获取 UUID 值。
    /// </summary>
    public Guid Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString("D");
}
