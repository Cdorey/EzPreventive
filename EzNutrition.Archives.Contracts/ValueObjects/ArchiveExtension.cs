using EzNutrition.Archives.Contracts.Internal;

namespace EzNutrition.Archives.Contracts.ValueObjects;

/// <summary>
/// 表示不改变核心字段含义的格式无关扩展。
/// </summary>
public sealed record ArchiveExtension
{
    private IReadOnlyList<ArchiveExtension> _children = Array.Empty<ArchiveExtension>();

    /// <summary>
    /// 初始化格式无关扩展。
    /// </summary>
    /// <param name="url">扩展定义的绝对 URI。</param>
    public ArchiveExtension(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (!url.IsAbsoluteUri)
        {
            throw new ArgumentException("扩展定义必须使用绝对 URI。", nameof(url));
        }

        Url = url;
    }

    /// <summary>
    /// 获取扩展定义的绝对 URI。
    /// </summary>
    public Uri Url { get; }

    /// <summary>
    /// 获取扩展的原子或结构化值。
    /// </summary>
    public ArchiveValue? Value { get; init; }

    /// <summary>
    /// 获取嵌套子扩展。
    /// </summary>
    /// <remarks>
    /// 一个扩展通常使用 <see cref="Value"/> 或 <see cref="Children"/> 之一；具体排他规则由验证器执行。
    /// </remarks>
    public IReadOnlyList<ArchiveExtension> Children
    {
        get => _children;
        init => _children = ArchiveCollections.Freeze(value);
    }
}
