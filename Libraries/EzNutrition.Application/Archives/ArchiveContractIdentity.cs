using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Archives.Contracts.Resources;

namespace EzNutrition.Application.Archives;

/// <summary>
/// 表示一个运行态档案资源的稳定逻辑身份和草稿版本身份。
/// </summary>
public sealed record ArchiveResourceIdentity
{
    /// <summary>
    /// 获取逻辑资源标识。
    /// </summary>
    public required ResourceId ResourceId { get; init; }

    /// <summary>
    /// 获取当前草稿版本标识。
    /// </summary>
    public required ResourceVersionId VersionId { get; init; }
}

/// <summary>
/// 保存一次运行态咨询映射到档案契约时使用的稳定资源身份。
/// </summary>
public sealed record ArchiveContractIdentity
{
    /// <summary>
    /// 获取咨询会话建立时间。
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>获取咨询对象资源身份。</summary>
    public required ArchiveResourceIdentity Patient { get; init; }

    /// <summary>获取咨询资源身份。</summary>
    public required ArchiveResourceIdentity Consultation { get; init; }

    /// <summary>获取能量评估资源身份。</summary>
    public required ArchiveResourceIdentity EnergyAssessment { get; init; }

    /// <summary>获取 DRIs 评估资源身份。</summary>
    public required ArchiveResourceIdentity DriAssessment { get; init; }

    /// <summary>获取膳食回忆资源身份。</summary>
    public required ArchiveResourceIdentity DietaryRecall { get; init; }

    /// <summary>获取 SOAP 资源身份。</summary>
    public required ArchiveResourceIdentity SoapNote { get; init; }

    /// <summary>获取营养建议资源身份。</summary>
    public required ArchiveResourceIdentity NutritionAdvice { get; init; }

    /// <summary>
    /// 建立一组新的运行态档案身份。
    /// </summary>
    /// <param name="createdAt">咨询会话建立时间；未提供时使用当前 UTC 时间。</param>
    /// <param name="guidFactory">UUID 生成器；未提供时使用 <see cref="Guid.NewGuid"/>。</param>
    /// <returns>新的档案身份集合。</returns>
    public static ArchiveContractIdentity Create(
        DateTimeOffset? createdAt = null,
        Func<Guid>? guidFactory = null)
    {
        guidFactory ??= Guid.NewGuid;
        return CreateCore(createdAt ?? DateTimeOffset.UtcNow, null, guidFactory);
    }

    internal static ArchiveContractIdentity CreateForPatient(
        PatientResource patient,
        DateTimeOffset? createdAt = null,
        Func<Guid>? guidFactory = null)
    {
        ArgumentNullException.ThrowIfNull(patient);
        guidFactory ??= Guid.NewGuid;
        var patientIdentity = new ArchiveResourceIdentity
        {
            ResourceId = patient.Metadata.ResourceId,
            VersionId = patient.Metadata.VersionId
        };
        return CreateCore(createdAt ?? DateTimeOffset.UtcNow, patientIdentity, guidFactory);
    }

    private static ArchiveContractIdentity CreateCore(
        DateTimeOffset createdAt,
        ArchiveResourceIdentity? patientIdentity,
        Func<Guid> guidFactory)
    {
        return new ArchiveContractIdentity
        {
            CreatedAt = createdAt,
            Patient = patientIdentity ?? CreateResourceIdentity(guidFactory),
            Consultation = CreateResourceIdentity(guidFactory),
            EnergyAssessment = CreateResourceIdentity(guidFactory),
            DriAssessment = CreateResourceIdentity(guidFactory),
            DietaryRecall = CreateResourceIdentity(guidFactory),
            SoapNote = CreateResourceIdentity(guidFactory),
            NutritionAdvice = CreateResourceIdentity(guidFactory)
        };
    }

    private static ArchiveResourceIdentity CreateResourceIdentity(Func<Guid> guidFactory) => new()
    {
        ResourceId = new ResourceId(guidFactory()),
        VersionId = new ResourceVersionId(guidFactory())
    };
}
