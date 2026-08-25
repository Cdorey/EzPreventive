namespace EzNutrition.Domain.Dietary;

/// <summary>
/// 表示一次膳食回顾核算产生的完整结果，结果在明确应用到调查对象前不会改变其运行态状态。
/// </summary>
public sealed record DietaryRecallCalculationResult
{
    /// <summary>获取按营养素、餐次和食物汇总的核算表。</summary>
    public required SummaryCalculationTable Summary { get; init; }

    /// <summary>获取用于界面解释和参考范围比较的营养素评估。</summary>
    public required IReadOnlyList<DietaryNutrientAssessment> NutrientAssessments { get; init; }

    /// <summary>获取每条膳食记录的营养素核算明细。</summary>
    public required IReadOnlyList<DietaryRecallEntryCalculation> EntryCalculations { get; init; }
}
