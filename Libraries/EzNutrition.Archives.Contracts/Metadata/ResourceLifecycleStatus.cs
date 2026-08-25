namespace EzNutrition.Archives.Contracts.Metadata;

/// <summary>
/// 指定档案资源的生命周期状态。
/// </summary>
public enum ResourceLifecycleStatus
{
    /// <summary>
    /// 可继续编辑和覆盖的草稿。
    /// </summary>
    Draft = 0,

    /// <summary>
    /// 已确认且不可原地修改的正式记录。
    /// </summary>
    Final = 1,

    /// <summary>
    /// 对既往正式记录作出的不可变修订版本。
    /// </summary>
    Amended = 2,

    /// <summary>
    /// 记录被错误建立，但仍保留其历史事实。
    /// </summary>
    EnteredInError = 3
}
