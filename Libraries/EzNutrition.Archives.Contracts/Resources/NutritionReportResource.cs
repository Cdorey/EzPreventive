using EzNutrition.Archives.Contracts.Abstractions;
using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Archives.Contracts.Internal;
using EzNutrition.Archives.Contracts.Metadata;
using EzNutrition.Archives.Contracts.ValueObjects;

namespace EzNutrition.Archives.Contracts.Resources;

/// <summary>
/// 表示人员以特定职责参与一份营养报告的事实快照。
/// </summary>
/// <remarks>
/// 同一主体可以分别作为作者、复核者或监督者出现；报告的正式签发者由
/// <see cref="ResourceMetadata.FinalizedBy"/> 表达。职责使用编码而非权限声明，
/// 以避免把宿主当前的授权状态固化为档案事实。
/// </remarks>
public sealed record ReportParticipation
{
    /// <summary>
    /// 获取参与职责编码，例如作者、复核者或教学监督者。
    /// </summary>
    public required Coding Function { get; init; }

    /// <summary>
    /// 获取承担该职责的人员或机构。
    /// </summary>
    public required ActorReference Actor { get; init; }

    /// <summary>
    /// 获取该参与行为发生的可选时间。
    /// </summary>
    /// <remarks>
    /// 修订版本可以保留早于当前版本建立时间的原作者或原复核行为，但该时间不得晚于
    /// 当前版本的最后修改时间。
    /// </remarks>
    public DateTimeOffset? ActedAt { get; init; }
}

/// <summary>
/// 表示报告契约所对应的外部渲染产物身份。
/// </summary>
/// <remarks>
/// 档案只保存媒体类型和内容指纹，不在该值对象中内嵌 PDF、HTML 或打印字节。
/// </remarks>
public sealed record ReportArtifactIdentity
{
    /// <summary>
    /// 初始化报告渲染产物身份。
    /// </summary>
    /// <param name="mediaType">产物媒体类型，例如 <c>application/pdf</c>。</param>
    /// <param name="fingerprint">按产物确切字节计算的内容指纹。</param>
    public ReportArtifactIdentity(string mediaType, ContentFingerprint fingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);
        ArgumentNullException.ThrowIfNull(fingerprint);
        MediaType = mediaType.Trim();
        Fingerprint = fingerprint;
    }

    /// <summary>
    /// 获取渲染产物媒体类型。
    /// </summary>
    public string MediaType { get; }

    /// <summary>
    /// 获取渲染产物确切字节的内容指纹。
    /// </summary>
    public ContentFingerprint Fingerprint { get; }
}

/// <summary>
/// 表示由一次咨询的精确资源版本形成的营养报告清单和签发上下文。
/// </summary>
/// <remarks>
/// 本资源描述报告的语义来源和责任主体，不规定具体打印版式。草稿可以没有复核者
/// 和渲染产物；正式或修订报告应通过资源元数据记录签发者，并绑定确切渲染产物。
/// </remarks>
public sealed record NutritionReportResource : IArchiveResource
{
    private IReadOnlyList<VersionedResourceReference> _inputResourceReferences =
        Array.Empty<VersionedResourceReference>();
    private IReadOnlyList<ReportParticipation> _participants = Array.Empty<ReportParticipation>();

    /// <inheritdoc />
    public ResourceTypeCode ResourceType => ArchiveResourceTypes.NutritionReport;

    /// <inheritdoc />
    public required ResourceMetadata Metadata { get; init; }

    /// <summary>
    /// 获取报告对象的逻辑资源引用。
    /// </summary>
    public required LogicalResourceReference SubjectReference { get; init; }

    /// <summary>
    /// 获取报告所属咨询的确切版本引用。
    /// </summary>
    public required VersionedResourceReference ConsultationReference { get; init; }

    /// <summary>
    /// 获取报告用途编码，例如医疗服务或教学。
    /// </summary>
    public required Coding Purpose { get; init; }

    /// <summary>
    /// 获取可选报告标题。
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// 获取形成报告内容的确切资源版本。
    /// </summary>
    public IReadOnlyList<VersionedResourceReference> InputResourceReferences
    {
        get => _inputResourceReferences;
        init => _inputResourceReferences = ArchiveCollections.Freeze(value);
    }

    /// <summary>
    /// 获取报告使用的可选版式模板及版本。
    /// </summary>
    public CanonicalReference? PresentationTemplate { get; init; }

    /// <summary>
    /// 获取已产生的可选外部渲染产物身份。
    /// </summary>
    public ReportArtifactIdentity? RenderedArtifact { get; init; }

    /// <summary>
    /// 获取报告作者、复核者或监督者等参与事实。
    /// </summary>
    public IReadOnlyList<ReportParticipation> Participants
    {
        get => _participants;
        init => _participants = ArchiveCollections.Freeze(value);
    }
}
