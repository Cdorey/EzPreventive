namespace EzNutrition.Archives.Contracts.ValueObjects;

/// <summary>
/// 指定测量值或评估输入的来源类型。
/// </summary>
public enum ClinicalValueSourceKind
{
    /// <summary>
    /// 由专业人员或设备直接测量。
    /// </summary>
    Measured = 0,

    /// <summary>
    /// 由咨询对象或其他人员报告。
    /// </summary>
    Reported = 1,

    /// <summary>
    /// 由专业人员估计。
    /// </summary>
    Estimated = 2,

    /// <summary>
    /// 由其他值计算或推导。
    /// </summary>
    Derived = 3,

    /// <summary>
    /// 从外部档案或系统导入。
    /// </summary>
    Imported = 4,

    /// <summary>
    /// 无法确定来源。
    /// </summary>
    Unknown = 5
}

/// <summary>
/// 表示具有来源和有效时间的临床测量值。
/// </summary>
public sealed record ClinicalMeasurement
{
    /// <summary>
    /// 获取测量数量。
    /// </summary>
    public required Quantity Value { get; init; }

    /// <summary>
    /// 获取该测量实际生效或采集的时间。
    /// </summary>
    public DateTimeOffset? EffectiveAt { get; init; }

    /// <summary>
    /// 获取测量来源类型。
    /// </summary>
    public required ClinicalValueSourceKind SourceKind { get; init; }

    /// <summary>
    /// 获取可选来源说明。
    /// </summary>
    public string? SourceDescription { get; init; }
}

/// <summary>
/// 表示评估参数的基础值、最终采用值及专业调整说明。
/// </summary>
public sealed record AssessmentInput
{
    /// <summary>
    /// 获取参数的稳定编码。
    /// </summary>
    public required Coding Parameter { get; init; }

    /// <summary>
    /// 获取调整前的基础值；没有适用基础值时可以为空。
    /// </summary>
    public ArchiveValue? BasisValue { get; init; }

    /// <summary>
    /// 获取计算或专业决定实际采用的值。
    /// </summary>
    public required ArchiveValue AdoptedValue { get; init; }

    /// <summary>
    /// 获取采用值的来源类型。
    /// </summary>
    public required ClinicalValueSourceKind SourceKind { get; init; }

    /// <summary>
    /// 获取从基础值推导采用值时使用的方法编码。
    /// </summary>
    public Coding? DerivationMethod { get; init; }

    /// <summary>
    /// 获取基础值与采用值存在实质差异时的专业理由。
    /// </summary>
    public string? AdjustmentReason { get; init; }
}

/// <summary>
/// 表示一个带稳定代码的计算结果或中间结果。
/// </summary>
public sealed record NamedArchiveValue
{
    /// <summary>
    /// 获取结果含义的稳定编码。
    /// </summary>
    public required Coding Name { get; init; }

    /// <summary>
    /// 获取结果值。
    /// </summary>
    public required ArchiveValue Value { get; init; }
}

/// <summary>
/// 表示算法、公式或选择器的稳定身份。
/// </summary>
public sealed record AlgorithmIdentity
{
    /// <summary>
    /// 获取方法编码；编码版本应标识算法版本。
    /// </summary>
    public required Coding Method { get; init; }

    /// <summary>
    /// 获取可选实现应用身份。
    /// </summary>
    public ApplicationIdentity? Implementation { get; init; }
}
