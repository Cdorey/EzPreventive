using EzNutrition.Domain.Assessments;
using EzNutrition.Domain.Consultations;
using EzNutrition.Shared.Data.Entities;

namespace EzNutrition.Application.Reports;

/// <summary>保存已取得的 DRIs 结果；不在打印时重新查询或重算。</summary>
public sealed record DriEvaluationSnapshot
{
    /// <summary>获取本次查询采用的性别。</summary>
    public required string Gender { get; init; }
    /// <summary>获取本次查询采用的实足年龄。</summary>
    public required ChronologicalAge Age { get; init; }
    /// <summary>获取本次查询采用的特殊生理时期。</summary>
    public required string SpecialPeriod { get; init; }
    /// <summary>获取成功取得结果的时间。</summary>
    public required DateTimeOffset RetrievedAt { get; init; }
    /// <summary>获取已捕获的参考值，未核定值保留为空，不能当作零。</summary>
    public required IReadOnlyList<DriEvaluationValue> Values { get; init; }
    /// <summary>获取聚合问题，包括无法形成明细行的营养素。</summary>
    public required IReadOnlyList<DriAggregationIssue> Issues { get; init; }

    /// <summary>在查询成功后复制条件、数值、单位和问题；后续页面修改不改变本次结果。</summary>
    public static DriEvaluationSnapshot Capture(DRIs result, DateTimeOffset retrievedAt)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Client.Age is not { } age || string.IsNullOrWhiteSpace(result.Client.Gender))
            throw new InvalidOperationException("DRIs 结果缺少明确的查询条件。");
        if (result.AvailableDRIs.Count == 0)
            throw new InvalidOperationException("当前没有可打印的 DRIs 参考结果。");

        var values = new List<DriEvaluationValue>();
        foreach (var range in result.NutrientRanges)
        {
            foreach (var value in new[] { range.EAR, range.RNI, range.UL }.OfType<AggregatedDriValue>())
                values.Add(new(range.Nutrient, value.RecordType, value.ResolvedValue, value.MeasureUnit,
                    string.Join("；", value.InnerRecords.Select(record => record.Detail).Where(detail => !string.IsNullOrWhiteSpace(detail)).Distinct())));
            foreach (var record in range.AdditionalReferenceGroups.SelectMany(group => group.Records))
                values.Add(new(range.Nutrient, record.RecordType, record.Value, record.MeasureUnit,
                    string.Join("；", new[] { record.IsOffset ? "调整量（非最终总量）" : null, record.Detail }
                        .Where(detail => !string.IsNullOrWhiteSpace(detail)))));
        }
        return new DriEvaluationSnapshot
        {
            Gender = result.Client.Gender,
            Age = age,
            SpecialPeriod = result.Client.SpecialPhysiologicalPeriod ?? "",
            RetrievedAt = retrievedAt,
            Values = values.AsReadOnly(),
            Issues = Array.AsReadOnly(result.AggregationIssues.ToArray())
        };
    }
}

/// <summary>表示一项已经捕获的参考结果，不包含分页或字体设置。</summary>
/// <param name="Nutrient">营养素名称。</param>
/// <param name="Type">参考指标，明确区分 AI、RNI 及 AMDR 上下限。</param>
/// <param name="Value">参考数值；无法核定时为空。</param>
/// <param name="Unit">原始计量单位。</param>
/// <param name="Note">来源说明及必要的调整量标记。</param>
public sealed record DriEvaluationValue(string Nutrient, DietaryReferenceIntakeType Type, decimal? Value, string? Unit, string Note);
