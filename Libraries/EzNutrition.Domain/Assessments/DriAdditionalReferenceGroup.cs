using EzNutrition.Shared.Data.Entities;

namespace EzNutrition.Domain.Assessments;

/// <summary>
/// 表示同一营养素下需要独立于 EAR、RNI/AI 和 UL 展示的一组专项参考指标。
/// </summary>
public sealed record DriAdditionalReferenceGroup
{
    private DriAdditionalReferenceGroup(
        DriAdditionalReferenceKind kind,
        string code,
        string displayName,
        IReadOnlyList<DietaryReferenceIntakeValue> records)
    {
        Kind = kind;
        Code = code;
        DisplayName = displayName;
        Records = records;
    }

    /// <summary>获取指标分组类型。</summary>
    public DriAdditionalReferenceKind Kind { get; }

    /// <summary>获取专业缩写。</summary>
    public string Code { get; }

    /// <summary>获取面向用户的指标名称。</summary>
    public string DisplayName { get; }

    /// <summary>获取属于该指标分组的原始记录。</summary>
    public IReadOnlyList<DietaryReferenceIntakeValue> Records { get; }

    internal static IReadOnlyList<DriAdditionalReferenceGroup> Create(
        IEnumerable<DietaryReferenceIntakeValue> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        return records
            .GroupBy(GetGroupKey)
            .OrderBy(group => group.Key.Order)
            .ThenBy(group => group.Key.Code, StringComparer.Ordinal)
            .Select(group => new DriAdditionalReferenceGroup(
                group.Key.Kind,
                group.Key.Code,
                group.Key.DisplayName,
                group.OrderBy(record => GetRecordOrder(record.RecordType)).ToArray()))
            .ToArray();
    }

    private static (DriAdditionalReferenceKind Kind, string Code, string DisplayName, int Order) GetGroupKey(
        DietaryReferenceIntakeValue record) => record.RecordType switch
        {
            DietaryReferenceIntakeType.AMDR_L or DietaryReferenceIntakeType.AMDR_H =>
                (DriAdditionalReferenceKind.AcceptableMacronutrientDistributionRange,
                    "AMDR",
                    "可接受宏量营养素分布范围",
                    0),
            DietaryReferenceIntakeType.PI_NCD =>
                (DriAdditionalReferenceKind.ProposedIntakeForChronicDiseasePrevention,
                    "PI-NCD",
                    "慢性病预防建议摄入量",
                    1),
            DietaryReferenceIntakeType.SPL =>
                (DriAdditionalReferenceKind.SpecificProposedLevel,
                    "SPL",
                    "特定建议值",
                    2),
            _ =>
                (DriAdditionalReferenceKind.Other,
                    record.RecordType.ToString().Replace('_', '-'),
                    "其他参考指标",
                    3)
        };

    private static int GetRecordOrder(DietaryReferenceIntakeType type) => type switch
    {
        DietaryReferenceIntakeType.AMDR_L => 0,
        DietaryReferenceIntakeType.AMDR_H => 1,
        _ => 2
    };
}

/// <summary>定义专项 DRIs 指标的展示分组。</summary>
public enum DriAdditionalReferenceKind
{
    /// <summary>可接受宏量营养素分布范围。</summary>
    AcceptableMacronutrientDistributionRange,

    /// <summary>慢性病预防建议摄入量。</summary>
    ProposedIntakeForChronicDiseasePrevention,

    /// <summary>特定建议值。</summary>
    SpecificProposedLevel,

    /// <summary>尚未单独建模的其他指标。</summary>
    Other
}
