using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Archives.Contracts.Metadata;

namespace EzNutrition.Archives.Contracts.Abstractions;

/// <summary>
/// 表示可以独立识别、保存、引用或交换的档案资源。
/// </summary>
/// <remarks>
/// 此接口只描述档案语义，不约束资源使用 XML、JSON、数据库或其他形式持久化。
/// </remarks>
public interface IArchiveResource
{
    /// <summary>
    /// 获取资源类型的稳定机器代码。
    /// </summary>
    ResourceTypeCode ResourceType { get; }

    /// <summary>
    /// 获取资源的身份、版本、状态和来源元数据。
    /// </summary>
    ResourceMetadata Metadata { get; }
}
