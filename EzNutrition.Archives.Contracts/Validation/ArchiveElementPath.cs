namespace EzNutrition.Archives.Contracts.Validation;

/// <summary>
/// 表示指向契约对象属性或集合元素的稳定逻辑路径。
/// </summary>
/// <remarks>
/// 路径由斜杠分隔的公共属性名和零基集合索引组成；字符 <c>~</c> 与 <c>/</c>
/// 分别编码为 <c>~0</c> 与 <c>~1</c>。
/// </remarks>
public sealed record ArchiveElementPath
{
    /// <summary>
    /// 初始化逻辑路径。
    /// </summary>
    /// <param name="value">例如 <c>/Meals/0/Entries/1/ReportedAmount</c> 的路径。</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> 的结构或转义无效。</exception>
    public ArchiveElementPath(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > 2048 || value[0] != '/' || value.Split('/').Skip(1).Any(string.IsNullOrEmpty))
        {
            throw new ArgumentException("逻辑路径必须以斜杠开头，并包含非空路径段。", nameof(value));
        }

        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '~')
            {
                continue;
            }

            if (++index >= value.Length || value[index] is not ('0' or '1'))
            {
                throw new ArgumentException("逻辑路径包含无效转义。", nameof(value));
            }
        }

        Value = value;
    }

    /// <summary>
    /// 获取规范化逻辑路径文本。
    /// </summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}
