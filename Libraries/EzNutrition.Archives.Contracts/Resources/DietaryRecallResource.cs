using EzNutrition.Archives.Contracts.Abstractions;
using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Archives.Contracts.Internal;
using EzNutrition.Archives.Contracts.Metadata;
using EzNutrition.Archives.Contracts.ValueObjects;

namespace EzNutrition.Archives.Contracts.Resources;

/// <summary>
/// 指定膳食回忆是否记录了摄入或明确的未摄入状态。
/// </summary>
public enum DietaryRecallStatus
{
    /// <summary>
    /// 已记录一个或多个摄入条目。
    /// </summary>
    IntakeReported = 0,

    /// <summary>
    /// 明确记录在回忆时间段内没有摄入。
    /// </summary>
    NoIntake = 1
}

/// <summary>
/// 表示膳食指南分类的观察结果和参考建议。
/// </summary>
public sealed record DietaryGuidanceItem
{
    private IReadOnlyList<DietaryGuidanceItem> _children = Array.Empty<DietaryGuidanceItem>();

    /// <summary>
    /// 获取膳食分类编码。
    /// </summary>
    public required Coding Category { get; init; }

    /// <summary>
    /// 获取本次膳食调查的观察结果。
    /// </summary>
    public ArchiveValue? ObservedValue { get; init; }

    /// <summary>
    /// 获取指南中的参考建议文本。
    /// </summary>
    public string? Recommendation { get; init; }

    /// <summary>
    /// 获取下级膳食分类。
    /// </summary>
    public IReadOnlyList<DietaryGuidanceItem> Children
    {
        get => _children;
        init => _children = ArchiveCollections.Freeze(value);
    }
}

/// <summary>
/// 表示膳食调查与膳食指南比较的历史快照。
/// </summary>
public sealed record DietaryGuidanceSnapshot
{
    private IReadOnlyList<DietaryGuidanceItem> _items = Array.Empty<DietaryGuidanceItem>();

    /// <summary>
    /// 获取比较方法和实现身份。
    /// </summary>
    public required AlgorithmIdentity Method { get; init; }

    /// <summary>
    /// 获取比较所采用的膳食指南身份。
    /// </summary>
    public ReferenceDataIdentity? Guideline { get; init; }

    /// <summary>
    /// 获取膳食指南身份缺失时的明确原因。
    /// </summary>
    public DataAbsentReasonCode? GuidelineAbsentReason { get; init; }

    /// <summary>
    /// 获取膳食分类比较结果。
    /// </summary>
    public IReadOnlyList<DietaryGuidanceItem> Items
    {
        get => _items;
        init => _items = ArchiveCollections.Freeze(value);
    }
}

/// <summary>
/// 表示某个营养素的数量结果。
/// </summary>
public sealed record NutrientAmount
{
    /// <summary>
    /// 获取营养素稳定编码。
    /// </summary>
    public required Coding Nutrient { get; init; }

    /// <summary>
    /// 获取营养素数量。
    /// </summary>
    public required Quantity Amount { get; init; }
}

/// <summary>
/// 表示膳食回忆中的一个食物摄入条目。
/// </summary>
public sealed record FoodIntakeEntry
{
    private IReadOnlyList<NutrientAmount> _nutrientContributions = Array.Empty<NutrientAmount>();

    /// <summary>
    /// 获取条目在所属膳食回忆资源内的稳定标识。
    /// </summary>
    public required LocalIdentifier EntryId { get; init; }

    /// <summary>
    /// 获取食物稳定编码及可选显示名。
    /// </summary>
    public required Coding Food { get; init; }

    /// <summary>
    /// 获取咨询对象最初报告的数量。
    /// </summary>
    public required Quantity ReportedAmount { get; init; }

    /// <summary>
    /// 获取可食比例，取值 0 至 1；未知时为 <see langword="null"/>。
    /// </summary>
    public decimal? EdibleFraction { get; init; }

    /// <summary>
    /// 获取最终用于营养计算的实际食用量；尚未完成换算时为空。
    /// </summary>
    public Quantity? AdoptedConsumedAmount { get; init; }

    /// <summary>
    /// 获取本条目使用的食物成分数据集身份；尚未核算或来源档案未提供时为空。
    /// </summary>
    public ReferenceDataIdentity? FoodCompositionData { get; init; }

    /// <summary>
    /// 获取正式记录缺少食物成分数据集身份时的明确原因。
    /// </summary>
    public DataAbsentReasonCode? FoodCompositionDataAbsentReason { get; init; }

    /// <summary>
    /// 获取该条目当时计算得到的营养素贡献快照。
    /// </summary>
    public IReadOnlyList<NutrientAmount> NutrientContributions
    {
        get => _nutrientContributions;
        init => _nutrientContributions = ArchiveCollections.Freeze(value);
    }

    /// <summary>
    /// 获取条目在同一餐次中的显示顺序。
    /// </summary>
    public int Sequence { get; init; }
}

/// <summary>
/// 表示一个餐次及其食物条目和营养素汇总。
/// </summary>
public sealed record MealRecall
{
    private IReadOnlyList<FoodIntakeEntry> _entries = Array.Empty<FoodIntakeEntry>();
    private IReadOnlyList<NutrientAmount> _nutrientSummary = Array.Empty<NutrientAmount>();

    /// <summary>
    /// 获取餐次在所属膳食回忆资源内的稳定标识。
    /// </summary>
    public required LocalIdentifier MealId { get; init; }

    /// <summary>
    /// 获取餐次编码，例如早餐、午餐或加餐。
    /// </summary>
    public required Coding Occasion { get; init; }

    /// <summary>
    /// 获取可选的实际进食时间。
    /// </summary>
    public DateTimeOffset? ConsumedAt { get; init; }

    /// <summary>
    /// 获取餐次显示顺序。
    /// </summary>
    public int Sequence { get; init; }

    /// <summary>
    /// 获取该餐次的食物摄入条目。
    /// </summary>
    public IReadOnlyList<FoodIntakeEntry> Entries
    {
        get => _entries;
        init => _entries = ArchiveCollections.Freeze(value);
    }

    /// <summary>
    /// 获取该餐次的历史营养素汇总快照。
    /// </summary>
    public IReadOnlyList<NutrientAmount> NutrientSummary
    {
        get => _nutrientSummary;
        init => _nutrientSummary = ArchiveCollections.Freeze(value);
    }
}

/// <summary>
/// 表示宏量营养素折算能量与记录总能量的一致性快照。
/// </summary>
public sealed record DietaryEnergyConsistency
{
    /// <summary>
    /// 获取折算方法和实现身份。
    /// </summary>
    public required AlgorithmIdentity Method { get; init; }

    /// <summary>
    /// 获取记录的全日总能量。
    /// </summary>
    public required Quantity RecordedTotalEnergy { get; init; }

    /// <summary>
    /// 获取由宏量营养素折算得到的能量。
    /// </summary>
    public required Quantity MacronutrientDerivedEnergy { get; init; }

    /// <summary>
    /// 获取允许的绝对差异；尚未确定容差策略时为空。
    /// </summary>
    public Quantity? AllowedDifference { get; init; }

    /// <summary>
    /// 获取容差缺失时的明确原因。
    /// </summary>
    public DataAbsentReasonCode? AllowedDifferenceAbsentReason { get; init; }

    /// <summary>
    /// 获取超出容差时的专业解释。
    /// </summary>
    public string? ProfessionalExplanation { get; init; }
}

/// <summary>
/// 表示一段时间内的膳食回忆、条目贡献和历史汇总结果。
/// </summary>
public sealed record DietaryRecallResource : IArchiveResource
{
    private IReadOnlyList<MealRecall> _meals = Array.Empty<MealRecall>();
    private IReadOnlyList<NutrientAmount> _totalNutrientSummary = Array.Empty<NutrientAmount>();

    /// <inheritdoc />
    public ResourceTypeCode ResourceType => ArchiveResourceTypes.DietaryRecall;

    /// <inheritdoc />
    public required ResourceMetadata Metadata { get; init; }

    /// <summary>
    /// 获取咨询对象的逻辑资源引用。
    /// </summary>
    public required LogicalResourceReference SubjectReference { get; init; }

    /// <summary>
    /// 获取所属咨询的可选确切版本引用。
    /// </summary>
    public VersionedResourceReference? ConsultationReference { get; init; }

    /// <summary>
    /// 获取膳食回忆覆盖的时间段；来源资料未记录时为空。
    /// </summary>
    public Period? RecallPeriod { get; init; }

    /// <summary>
    /// 获取回忆时间段缺失时的明确原因。
    /// </summary>
    public DataAbsentReasonCode? RecallPeriodAbsentReason { get; init; }

    /// <summary>
    /// 获取膳食调查方法编码，例如 24 小时膳食回顾法。
    /// </summary>
    public required Coding RecallMethod { get; init; }

    /// <summary>
    /// 获取回忆状态；草稿尚未记录摄入或未摄入结论时为空。
    /// </summary>
    public DietaryRecallStatus? Status { get; init; }

    /// <summary>
    /// 获取明确未摄入时的原因编码。
    /// </summary>
    public Coding? NoIntakeReason { get; init; }

    /// <summary>
    /// 获取餐次记录。
    /// </summary>
    public IReadOnlyList<MealRecall> Meals
    {
        get => _meals;
        init => _meals = ArchiveCollections.Freeze(value);
    }

    /// <summary>
    /// 获取全日或完整回忆时间段的历史营养素汇总快照。
    /// </summary>
    public IReadOnlyList<NutrientAmount> TotalNutrientSummary
    {
        get => _totalNutrientSummary;
        init => _totalNutrientSummary = ArchiveCollections.Freeze(value);
    }

    /// <summary>
    /// 获取宏量营养素能量一致性快照。
    /// </summary>
    public DietaryEnergyConsistency? EnergyConsistency { get; init; }

    /// <summary>
    /// 获取膳食调查与膳食指南比较的历史快照。
    /// </summary>
    public DietaryGuidanceSnapshot? GuidanceSnapshot { get; init; }
}
