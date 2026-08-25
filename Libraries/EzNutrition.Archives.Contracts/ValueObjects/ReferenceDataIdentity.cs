using EzNutrition.Archives.Contracts.Internal;

namespace EzNutrition.Archives.Contracts.ValueObjects;

/// <summary>
/// 表示内容指纹。
/// </summary>
public sealed record ContentFingerprint
{
    /// <summary>
    /// 初始化内容指纹。
    /// </summary>
    /// <param name="algorithm">哈希或指纹算法编码。</param>
    /// <param name="value">按算法规定编码的指纹文本。</param>
    public ContentFingerprint(Coding algorithm, string value)
    {
        ArgumentNullException.ThrowIfNull(algorithm);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Algorithm = algorithm;
        Value = value.Trim();
    }

    /// <summary>
    /// 获取算法编码。
    /// </summary>
    public Coding Algorithm { get; }

    /// <summary>
    /// 获取指纹文本。
    /// </summary>
    public string Value { get; }
}

/// <summary>
/// 表示计算所依赖的参考数据集、版本和可选内容指纹。
/// </summary>
public sealed record ReferenceDataIdentity
{
    private IReadOnlyList<CanonicalReference> _derivedFrom = Array.Empty<CanonicalReference>();

    /// <summary>
    /// 初始化参考数据集身份。
    /// </summary>
    /// <param name="system">控制数据集代码的绝对 URI。</param>
    /// <param name="code">数据集稳定机器代码。</param>
    public ReferenceDataIdentity(Uri system, string code)
    {
        ArgumentNullException.ThrowIfNull(system);
        if (!system.IsAbsoluteUri)
        {
            throw new ArgumentException("参考数据体系必须使用绝对 URI。", nameof(system));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        System = system;
        Code = code.Trim();
    }

    /// <summary>
    /// 获取数据集标识体系。
    /// </summary>
    public Uri System { get; }

    /// <summary>
    /// 获取数据集机器代码。
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// 获取可选版次。
    /// </summary>
    public string? Edition { get; init; }

    /// <summary>
    /// 获取可选发布版本。
    /// </summary>
    public string? Release { get; init; }

    /// <summary>
    /// 获取可选内容指纹。
    /// </summary>
    public ContentFingerprint? Fingerprint { get; init; }

    /// <summary>
    /// 获取无法提供指纹时的明确原因。
    /// </summary>
    public DataAbsentReasonCode? FingerprintAbsentReason { get; init; }

    /// <summary>
    /// 获取该数据集派生来源的规范引用。
    /// </summary>
    public IReadOnlyList<CanonicalReference> DerivedFrom
    {
        get => _derivedFrom;
        init => _derivedFrom = ArchiveCollections.Freeze(value);
    }
}
