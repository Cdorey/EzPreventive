using EzNutrition.Archives.Contracts.Internal;

namespace EzNutrition.Archives.Contracts.ValueObjects;

/// <summary>
/// 表示咨询对象姓名的结构化或显示形式。
/// </summary>
public sealed record HumanName
{
    private IReadOnlyList<string> _given = Array.Empty<string>();

    /// <summary>
    /// 获取可直接显示的完整姓名文本。
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// 获取姓氏或家族名。
    /// </summary>
    public string? Family { get; init; }

    /// <summary>
    /// 获取名字部分。
    /// </summary>
    public IReadOnlyList<string> Given
    {
        get => _given;
        init => _given = ArchiveCollections.Freeze(value);
    }

    /// <summary>
    /// 获取可选的姓名用途编码，例如正式姓名或曾用名。
    /// </summary>
    public Coding? Use { get; init; }
}
