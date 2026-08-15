using EzNutrition.Domain.Assessments;
using EzNutrition.Domain.Consultations;
using EzNutrition.Shared.Data.Entities;

namespace EzNutrition.Application.Tests.Assessments;

/// <summary>
/// 锁定 DRIs 聚合迁入领域层后的既有核算行为，并验证新增的显式解析状态。
/// </summary>
public sealed class DrisAggregationTests
{
    /// <summary>验证一个基础值与偏移值继续按既有规则相加。</summary>
    [Fact]
    public void Base_and_offset_records_are_summed()
    {
        var dris = CreateDris(
            Record("蛋白质", DietaryReferenceIntakeType.RNI, 60, "g/d"),
            Record("蛋白质", DietaryReferenceIntakeType.RNI, 10, "g/d", isOffset: true));

        var range = Assert.Single(dris.NutrientRanges);
        var rni = Assert.IsType<AggregatedDriValue>(range.RNI);

        Assert.True(rni.IsResolved);
        Assert.Equal(70, rni.ResolvedValue);
        Assert.Equal(70, rni.Value);
        Assert.Empty(dris.AggregationIssues);
    }

    /// <summary>验证特殊生理时期基础值继续覆盖同组通用基础值。</summary>
    [Fact]
    public void Special_period_base_record_overrides_general_base_record()
    {
        var dris = CreateDris(
            Record("铁", DietaryReferenceIntakeType.RNI, 18, "mg/d"),
            Record("铁", DietaryReferenceIntakeType.RNI, 24, "mg/d", period: "孕中期"));

        var rni = Assert.IsType<AggregatedDriValue>(Assert.Single(dris.NutrientRanges).RNI);

        Assert.Equal(24, rni.ResolvedValue);
        Assert.Single(rni.InnerRecords);
        Assert.Equal("孕中期", rni.InnerRecords[0].SpecialPhysiologicalPeriod);
    }

    /// <summary>验证无法选择唯一基础值时保留零值兼容行为，同时暴露显式冲突状态。</summary>
    [Fact]
    public void Conflicting_base_records_are_reported_without_throwing()
    {
        var dris = CreateDris(
            Record("锌", DietaryReferenceIntakeType.RNI, 10, "mg/d"),
            Record("锌", DietaryReferenceIntakeType.RNI, 12, "mg/d"));

        var rni = Assert.IsType<AggregatedDriValue>(Assert.Single(dris.NutrientRanges).RNI);

        Assert.False(rni.IsResolved);
        Assert.Null(rni.ResolvedValue);
        Assert.Equal(0, rni.Value);
        Assert.Single(dris.AggregationIssues);
    }

    /// <summary>验证合法零值不会被误报为聚合冲突。</summary>
    [Fact]
    public void A_resolved_zero_value_is_not_treated_as_a_conflict()
    {
        var dris = CreateDris(Record("合成营养素", DietaryReferenceIntakeType.EAR, 0, "mg/d"));

        var ear = Assert.IsType<AggregatedDriValue>(Assert.Single(dris.NutrientRanges).EAR);

        Assert.True(ear.IsResolved);
        Assert.Equal(0, ear.ResolvedValue);
        Assert.Equal("0 mg/d", ear.ToString());
        Assert.Empty(dris.AggregationIssues);
    }

    /// <summary>验证单位冲突被转换为领域问题，而不是越过边界抛给 UI。</summary>
    [Fact]
    public void Unit_conflicts_are_isolated_as_aggregation_issues()
    {
        var dris = CreateDris(
            Record("钙", DietaryReferenceIntakeType.RNI, 800, "mg/d"),
            Record("钙", DietaryReferenceIntakeType.RNI, 1, "g/d", isOffset: true));

        Assert.Empty(dris.NutrientRanges);
        var issue = Assert.Single(dris.AggregationIssues);
        Assert.Equal("钙", issue.Nutrient);
        Assert.Equal("同一参考指标使用了不同计量单位，无法自动核算。", issue.Message);
    }

    /// <summary>验证专项指标按 AMDR、PI-NCD 和 SPL 的专业含义分组。</summary>
    [Fact]
    public void Additional_reference_records_are_grouped_by_professional_meaning()
    {
        var dris = CreateDris(
            Record("总脂肪", DietaryReferenceIntakeType.AMDR_H, 30, "%E"),
            Record("总脂肪", DietaryReferenceIntakeType.SPL, 25, "%E"),
            Record("总脂肪", DietaryReferenceIntakeType.AMDR_L, 20, "%E"),
            Record("总脂肪", DietaryReferenceIntakeType.PI_NCD, 30, "%E"));

        var range = Assert.Single(dris.NutrientRanges);

        Assert.Equal(4, range.OtherRecords.Count);
        Assert.Equal(30m, range.PiNcd?.ResolvedValue);
        Assert.Equal("%E", range.PiNcd?.MeasureUnit);
        Assert.Collection(
            range.AdditionalReferenceGroups,
            group =>
            {
                Assert.Equal("AMDR", group.Code);
                Assert.Equal(
                    [DietaryReferenceIntakeType.AMDR_L, DietaryReferenceIntakeType.AMDR_H],
                    group.Records.Select(record => record.RecordType));
            },
            group => Assert.Equal("PI-NCD", group.Code),
            group => Assert.Equal("SPL", group.Code));
    }

    private static DRIs CreateDris(params DietaryReferenceIntakeValue[] records) => new(new ClientInfo())
    {
        AvailableDRIs = [.. records]
    };

    private static DietaryReferenceIntakeValue Record(
        string nutrient,
        DietaryReferenceIntakeType type,
        decimal value,
        string unit,
        bool isOffset = false,
        string? period = null) => new()
        {
            Nutrient = nutrient,
            RecordType = type,
            Value = value,
            MeasureUnit = unit,
            IsOffset = isOffset,
            SpecialPhysiologicalPeriod = period
        };
}
