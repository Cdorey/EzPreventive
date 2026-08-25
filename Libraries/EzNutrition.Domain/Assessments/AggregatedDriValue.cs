using EzNutrition.Shared.Data.Entities;

namespace EzNutrition.Domain.Assessments;

/// <summary>
/// 表示由一条基础记录和可选偏移记录核算出的单项 DRIs 参考值。
/// </summary>
public sealed class AggregatedDriValue : IDietaryReferenceIntakeValue
{
    private readonly decimal? resolvedValue;

    internal AggregatedDriValue(IEnumerable<DietaryReferenceIntakeValue> innerRecords)
    {
        ArgumentNullException.ThrowIfNull(innerRecords);

        var records = innerRecords.ToArray();
        if (records.Length == 0)
        {
            throw new DriAggregationException("没有可用于核算的参考记录。");
        }

        if (records.Select(record => record.Nutrient).Distinct().Count() != 1)
        {
            throw new DriAggregationException("记录包含多个营养素，无法合并。");
        }

        if (records.Select(record => record.MeasureUnit).Distinct().Count() != 1)
        {
            throw new DriAggregationException("同一参考指标使用了不同计量单位，无法自动核算。");
        }

        if (records.Select(record => record.RecordType).Distinct().Count() != 1 &&
            records.Any(record => record.RecordType is not DietaryReferenceIntakeType.AI and not DietaryReferenceIntakeType.RNI))
        {
            throw new DriAggregationException("记录包含无法合并的 DRIs 类型。");
        }

        InnerRecords = ResolveSpecialPeriodOverride(records);
        resolvedValue = InnerRecords.Count(record => !record.IsOffset) == 1
            ? InnerRecords.Sum(record => record.Value)
            : null;
    }

    /// <summary>获取参与核算的原始参考记录。</summary>
    public IReadOnlyList<DietaryReferenceIntakeValue> InnerRecords { get; }

    /// <summary>获取核算后的值；无法唯一确定基础记录时为 <see langword="null"/>。</summary>
    public decimal? ResolvedValue => resolvedValue;

    /// <summary>获取是否已经形成可用于自动判断的单一参考值。</summary>
    public bool IsResolved => resolvedValue.HasValue;

    /// <inheritdoc />
    public string? MeasureUnit => InnerRecords[0].MeasureUnit;

    /// <inheritdoc />
    public string? Nutrient => InnerRecords[0].Nutrient;

    /// <inheritdoc />
    public DietaryReferenceIntakeType RecordType =>
        InnerRecords.FirstOrDefault(record => !record.IsOffset)?.RecordType ?? InnerRecords[0].RecordType;

    /// <inheritdoc />
    /// <remarks>
    /// 为兼容现有膳食核算调用方，未解析值继续通过该属性返回零；新代码应优先检查
    /// <see cref="ResolvedValue"/> 或 <see cref="IsResolved"/>。
    /// </remarks>
    public decimal Value => resolvedValue ?? default;

    /// <inheritdoc />
    public override string ToString() => resolvedValue is null
        ? "数据存在冲突，需手工核定"
        : $"{FormatValue(resolvedValue.Value)} {MeasureUnit}{(RecordType == DietaryReferenceIntakeType.AI ? " (AI)" : string.Empty)}";

    private static IReadOnlyList<DietaryReferenceIntakeValue> ResolveSpecialPeriodOverride(
        IReadOnlyList<DietaryReferenceIntakeValue> records)
    {
        // 保留既有规则：当两个基础值中恰有一个属于特殊生理时期时，以特殊时期值覆盖通用值。
        var absoluteRecords = records.Where(record => !record.IsOffset).ToArray();
        var hasSpecialPeriodOverride = absoluteRecords.Length == 2 &&
            absoluteRecords.Count(record => record.SpecialPhysiologicalPeriod is not null) == 1;

        if (!hasSpecialPeriodOverride)
        {
            return records.ToArray();
        }

        return records
            .Where(record => record.IsOffset || record.SpecialPhysiologicalPeriod is not null)
            .ToArray();
    }

    private static string FormatValue(decimal value) =>
        value % 1 == 0 ? decimal.Truncate(value).ToString("0") : value.ToString();
}

/// <summary>表示来源数据无法按既定 DRIs 规则完成聚合。</summary>
internal sealed class DriAggregationException(string message) : Exception(message);
