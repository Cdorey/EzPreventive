using EzNutrition.Archives.Contracts.Abstractions;
using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Archives.Contracts.Internal;
using EzNutrition.Archives.Contracts.Metadata;
using EzNutrition.Archives.Contracts.ValueObjects;

namespace EzNutrition.Archives.Contracts.Resources;

/// <summary>
/// 指定咨询对象与外部身份的关联模式。
/// </summary>
public enum PatientIdentityMode
{
    /// <summary>
    /// 具有可识别姓名或业务标识。
    /// </summary>
    Identified = 0,

    /// <summary>
    /// 使用稳定假名或研究标识，但不在本档案中直接保存真实身份。
    /// </summary>
    Pseudonymous = 1,

    /// <summary>
    /// 未与任何可识别外部身份建立关联。
    /// </summary>
    Unlinked = 2
}

/// <summary>
/// 表示咨询对象的稳定身份及非咨询时点资料。
/// </summary>
/// <remarks>
/// 年龄、身高、体重、孕期、PAL、BMI 和计算结果不属于本资源的核心资料。
/// </remarks>
public sealed record PatientResource : IArchiveResource
{
    private IReadOnlyList<HumanName> _names = Array.Empty<HumanName>();
    private IReadOnlyList<BusinessIdentifier> _businessIdentifiers = Array.Empty<BusinessIdentifier>();

    /// <inheritdoc />
    public ResourceTypeCode ResourceType => ArchiveResourceTypes.Patient;

    /// <inheritdoc />
    public required ResourceMetadata Metadata { get; init; }

    /// <summary>
    /// 获取身份关联模式。
    /// </summary>
    public required PatientIdentityMode IdentityMode { get; init; }

    /// <summary>
    /// 获取可选姓名；咨询对象可以没有姓名。
    /// </summary>
    public IReadOnlyList<HumanName> Names
    {
        get => _names;
        init => _names = ArchiveCollections.Freeze(value);
    }

    /// <summary>
    /// 获取由医院、社区或其他机构签发的可选业务标识。
    /// </summary>
    public IReadOnlyList<BusinessIdentifier> BusinessIdentifiers
    {
        get => _businessIdentifiers;
        init => _businessIdentifiers = ArchiveCollections.Freeze(value);
    }

    /// <summary>
    /// 获取具有真实精度的出生日期。
    /// </summary>
    public PartialDate? BirthDate { get; init; }

    /// <summary>
    /// 获取行政登记性别编码。
    /// </summary>
    /// <remarks>
    /// 该属性不得替代具体评估中实际采用的生理分类或参考人群分类。
    /// </remarks>
    public Coding? AdministrativeSex { get; init; }

    /// <summary>
    /// 获取管理该咨询对象档案的可选机构引用。
    /// </summary>
    public ActorReference? ManagingOrganization { get; init; }
}
