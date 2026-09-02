using EzNutrition.Shared.Data.Entities;

namespace EzNutrition.Application.Ports;

/// <summary>
/// 表示查询营养参考数据所需的咨询对象条件。
/// </summary>
public sealed record NutritionSubjectQuery
{
    /// <summary>获取行政登记性别或当前数据源采用的性别分类。</summary>
    public required string Gender { get; init; }

    /// <summary>获取用于匹配参考数据年龄阈值的十进制年。</summary>
    public required decimal AgeInYears { get; init; }

    /// <summary>获取特殊生理时期；无特殊时期时为空字符串。</summary>
    public string SpecialPhysiologicalPeriod { get; init; } = string.Empty;
}

/// <summary>
/// 提供能量参考记录。
/// </summary>
public interface IEnergyReferenceDataSource
{
    /// <summary>读取符合咨询对象条件的能量参考记录。</summary>
    Task<IReadOnlyList<EER>> GetEnergyReferencesAsync(
        NutritionSubjectQuery subject,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 提供膳食参考摄入量记录。
/// </summary>
public interface IDietaryReferenceIntakeDataSource
{
    /// <summary>读取符合咨询对象条件的膳食参考摄入量记录。</summary>
    Task<IReadOnlyList<DietaryReferenceIntakeValue>> GetDietaryReferenceIntakesAsync(
        NutritionSubjectQuery subject,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 提供食物目录、营养素目录和单个食物的成分明细。
/// </summary>
public interface IFoodCompositionDataSource
{
    /// <summary>读取可供选择的食物目录。</summary>
    Task<IReadOnlyList<Food>> GetFoodsAsync(CancellationToken cancellationToken = default);

    /// <summary>读取可供核算的营养素目录。</summary>
    Task<IReadOnlyList<Nutrient>> GetNutrientsAsync(CancellationToken cancellationToken = default);

    /// <summary>读取指定食物代码的营养成分明细。</summary>
    Task<IReadOnlyList<FoodNutrientValue>> GetFoodCompositionAsync(
        string friendlyCode,
        CancellationToken cancellationToken = default);
}
