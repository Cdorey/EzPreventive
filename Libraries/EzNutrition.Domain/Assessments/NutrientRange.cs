using EzNutrition.Shared.Data.Entities;

namespace EzNutrition.Domain.Assessments;

/// <summary>
/// 表示同一营养素的核心参考值和专项参考指标。
/// </summary>
public sealed class NutrientRange
{
    /// <summary>使用同一营养素的原始记录建立参考值集合。</summary>
    public NutrientRange(IGrouping<string, DietaryReferenceIntakeValue> innerRecords)
    {
        ArgumentNullException.ThrowIfNull(innerRecords);

        var records = innerRecords.ToArray();
        Nutrient = innerRecords.Key;
        EAR = CreateReferenceValue(records, DietaryReferenceIntakeType.EAR);
        RNI = CreateReferenceValue(records, DietaryReferenceIntakeType.RNI, DietaryReferenceIntakeType.AI);
        UL = CreateReferenceValue(records, DietaryReferenceIntakeType.UL);
        PiNcd = CreateReferenceValue(records, DietaryReferenceIntakeType.PI_NCD);
        OtherRecords = records
            .Where(record => record.RecordType is not DietaryReferenceIntakeType.EAR
                and not DietaryReferenceIntakeType.RNI
                and not DietaryReferenceIntakeType.AI
                and not DietaryReferenceIntakeType.UL)
            .ToArray();
        AdditionalReferenceGroups = DriAdditionalReferenceGroup.Create(OtherRecords);
    }

    /// <summary>获取营养素名称。</summary>
    public string Nutrient { get; }

    /// <summary>获取平均需要量。</summary>
    public AggregatedDriValue? EAR { get; }

    /// <summary>获取推荐摄入量或适宜摄入量。</summary>
    public AggregatedDriValue? RNI { get; }

    /// <summary>获取可耐受最高摄入量。</summary>
    public AggregatedDriValue? UL { get; }

    /// <summary>获取慢性病预防建议摄入量。</summary>
    public AggregatedDriValue? PiNcd { get; }

    /// <summary>获取尚未归入核心参考值的原始记录。</summary>
    public IReadOnlyList<DietaryReferenceIntakeValue> OtherRecords { get; }

    /// <summary>获取按专业含义归组的专项参考指标。</summary>
    public IReadOnlyList<DriAdditionalReferenceGroup> AdditionalReferenceGroups { get; }

    private static AggregatedDriValue? CreateReferenceValue(
        IEnumerable<DietaryReferenceIntakeValue> records,
        params DietaryReferenceIntakeType[] types)
    {
        var matchingRecords = records.Where(record => types.Contains(record.RecordType)).ToArray();
        return matchingRecords.Length == 0 ? null : new AggregatedDriValue(matchingRecords);
    }
}
