namespace EzNutrition.Archives.Contracts.Identity;

/// <summary>
/// 表示对逻辑资源的引用，不固定其确切版本。
/// </summary>
public sealed record LogicalResourceReference
{
    /// <summary>
    /// 初始化逻辑资源引用。
    /// </summary>
    /// <param name="resourceId">逻辑资源标识。</param>
    /// <param name="expectedResourceType">可选的预期资源类型。</param>
    public LogicalResourceReference(ResourceId resourceId, ResourceTypeCode? expectedResourceType = null)
    {
        ArgumentNullException.ThrowIfNull(resourceId);
        ResourceId = resourceId;
        ExpectedResourceType = expectedResourceType;
    }

    /// <summary>
    /// 获取逻辑资源标识。
    /// </summary>
    public ResourceId ResourceId { get; }

    /// <summary>
    /// 获取预期资源类型；未声明时为 <see langword="null"/>。
    /// </summary>
    public ResourceTypeCode? ExpectedResourceType { get; }
}

/// <summary>
/// 表示对某个资源确切版本的引用。
/// </summary>
public sealed record VersionedResourceReference
{
    /// <summary>
    /// 初始化确切版本引用。
    /// </summary>
    /// <param name="resourceId">逻辑资源标识。</param>
    /// <param name="versionId">资源版本标识。</param>
    /// <param name="expectedResourceType">可选的预期资源类型。</param>
    public VersionedResourceReference(
        ResourceId resourceId,
        ResourceVersionId versionId,
        ResourceTypeCode? expectedResourceType = null)
    {
        ArgumentNullException.ThrowIfNull(resourceId);
        ArgumentNullException.ThrowIfNull(versionId);
        ResourceId = resourceId;
        VersionId = versionId;
        ExpectedResourceType = expectedResourceType;
    }

    /// <summary>
    /// 获取逻辑资源标识。
    /// </summary>
    public ResourceId ResourceId { get; }

    /// <summary>
    /// 获取资源版本标识。
    /// </summary>
    public ResourceVersionId VersionId { get; }

    /// <summary>
    /// 获取预期资源类型；未声明时为 <see langword="null"/>。
    /// </summary>
    public ResourceTypeCode? ExpectedResourceType { get; }
}
