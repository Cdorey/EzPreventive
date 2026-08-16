namespace EzNutrition.Archives.Contracts.Identity;

/// <summary>
/// 表示资源类型的稳定机器代码。
/// </summary>
public sealed record ResourceTypeCode
{
    /// <summary>
    /// 初始化资源类型代码。
    /// </summary>
    /// <param name="value">以英文字母开头，仅包含英文字母、数字或连字符的代码。</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> 不是合法代码。</exception>
    public ResourceTypeCode(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var trimmed = value.Trim();
        if (trimmed.Length > 64 || !char.IsAsciiLetter(trimmed[0]) ||
            trimmed.Any(static character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
        {
            throw new ArgumentException("资源类型代码必须以英文字母开头，且只能包含英文字母、数字或连字符。", nameof(value));
        }

        Value = trimmed;
    }

    /// <summary>
    /// 获取代码文本。
    /// </summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>
/// 提供 EzNutrition 首期已知资源类型代码。
/// </summary>
public static class ArchiveResourceTypes
{
    /// <summary>
    /// 获取咨询对象资源类型。
    /// </summary>
    public static ResourceTypeCode Patient { get; } = new("Patient");

    /// <summary>
    /// 获取咨询资源类型。
    /// </summary>
    public static ResourceTypeCode Consultation { get; } = new("Consultation");

    /// <summary>
    /// 获取能量评估资源类型。
    /// </summary>
    public static ResourceTypeCode EnergyAssessment { get; } = new("EnergyAssessment");

    /// <summary>
    /// 获取膳食参考摄入量评估资源类型。
    /// </summary>
    public static ResourceTypeCode DriAssessment { get; } = new("DriAssessment");

    /// <summary>
    /// 获取膳食回忆资源类型。
    /// </summary>
    public static ResourceTypeCode DietaryRecall { get; } = new("DietaryRecall");

    /// <summary>
    /// 获取 SOAP 病史资源类型。
    /// </summary>
    public static ResourceTypeCode SoapNote { get; } = new("SoapNote");

    /// <summary>
    /// 获取营养建议资源类型。
    /// </summary>
    public static ResourceTypeCode NutritionAdvice { get; } = new("NutritionAdvice");

    /// <summary>
    /// 获取营养报告资源类型。
    /// </summary>
    public static ResourceTypeCode NutritionReport { get; } = new("NutritionReport");
}
