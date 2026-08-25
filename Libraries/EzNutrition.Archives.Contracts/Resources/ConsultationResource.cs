using EzNutrition.Archives.Contracts.Abstractions;
using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Archives.Contracts.Internal;
using EzNutrition.Archives.Contracts.Metadata;
using EzNutrition.Archives.Contracts.ValueObjects;

namespace EzNutrition.Archives.Contracts.Resources;

/// <summary>
/// 表示咨询发生时为解释和复核而保存的咨询对象资料快照。
/// </summary>
/// <remarks>
/// 快照建立后不再与 <see cref="PatientResource"/> 的当前版本实时绑定。
/// </remarks>
public sealed record SubjectSnapshot
{
    private IReadOnlyList<Coding> _physiologicalStates = Array.Empty<Coding>();

    /// <summary>
    /// 获取咨询时采用的结构化实足年龄。
    /// </summary>
    public ChronologicalAge? ChronologicalAgeAtConsultation { get; init; }

    /// <summary>
    /// 获取旧版咨询快照采用的单一数量年龄。
    /// </summary>
    /// <remarks>
    /// 新档案同时提供结构化年龄和该整岁降级值，以便旧版读取器安全调阅；
    /// 新代码应优先使用 <see cref="ChronologicalAgeAtConsultation"/>。
    /// </remarks>
    public Quantity? AgeAtConsultation { get; init; }

    /// <summary>
    /// 获取咨询时的行政登记性别快照。
    /// </summary>
    public Coding? AdministrativeSex { get; init; }

    /// <summary>
    /// 获取咨询时身高。
    /// </summary>
    public ClinicalMeasurement? Height { get; init; }

    /// <summary>
    /// 获取咨询时体重。
    /// </summary>
    public ClinicalMeasurement? Weight { get; init; }

    /// <summary>
    /// 获取咨询时腰围。
    /// </summary>
    public ClinicalMeasurement? WaistCircumference { get; init; }

    /// <summary>
    /// 获取咨询时臀围。
    /// </summary>
    public ClinicalMeasurement? HipCircumference { get; init; }

    /// <summary>
    /// 获取咨询时存在的生理状态编码；可以同时存在多个状态。
    /// </summary>
    public IReadOnlyList<Coding> PhysiologicalStates
    {
        get => _physiologicalStates;
        init => _physiologicalStates = ArchiveCollections.Freeze(value);
    }

    /// <summary>
    /// 获取仅供历史界面识别的可选身份显示文本。
    /// </summary>
    public string? IdentityDisplay { get; init; }
}

/// <summary>
/// 表示一次营养咨询的时间、对象快照和组成资源清单。
/// </summary>
public sealed record ConsultationResource : IArchiveResource
{
    private IReadOnlyList<VersionedResourceReference> _clinicalResourceReferences =
        Array.Empty<VersionedResourceReference>();
    private IReadOnlyList<Coding> _reasons = Array.Empty<Coding>();

    /// <inheritdoc />
    public ResourceTypeCode ResourceType => ArchiveResourceTypes.Consultation;

    /// <inheritdoc />
    public required ResourceMetadata Metadata { get; init; }

    /// <summary>
    /// 获取咨询对象的逻辑资源引用。
    /// </summary>
    public required LogicalResourceReference SubjectReference { get; init; }

    /// <summary>
    /// 获取咨询开始和可选结束时间。
    /// </summary>
    public required Period Period { get; init; }

    /// <summary>
    /// 获取咨询时的对象资料快照。
    /// </summary>
    public SubjectSnapshot? SubjectSnapshot { get; init; }

    /// <summary>
    /// 获取本次咨询固定引用的临床资源确切版本。
    /// </summary>
    public IReadOnlyList<VersionedResourceReference> ClinicalResourceReferences
    {
        get => _clinicalResourceReferences;
        init => _clinicalResourceReferences = ArchiveCollections.Freeze(value);
    }

    /// <summary>
    /// 获取可选咨询标题。
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// 获取咨询原因或目的编码。
    /// </summary>
    public IReadOnlyList<Coding> Reasons
    {
        get => _reasons;
        init => _reasons = ArchiveCollections.Freeze(value);
    }

    /// <summary>
    /// 获取可选服务提供机构或人员。
    /// </summary>
    public ActorReference? ServiceProvider { get; init; }

    /// <summary>
    /// 获取可选地点显示文本。
    /// </summary>
    public string? LocationDisplay { get; init; }
}
