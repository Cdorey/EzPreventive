namespace EzNutrition.Domain.Assessments;

/// <summary>
/// 表示一次量表作答中的一个不可变、类型化回答。
/// </summary>
public abstract class NutritionAssessmentAnswer
{
    private protected NutritionAssessmentAnswer()
    {
    }
}

/// <summary>
/// 表示单选题所选择的一个稳定选项编码。
/// </summary>
public sealed class NutritionAssessmentSingleChoiceAnswer : NutritionAssessmentAnswer
{
    /// <summary>使用选项稳定编码建立回答。</summary>
    public NutritionAssessmentSingleChoiceAnswer(string optionCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(optionCode);
        OptionCode = optionCode;
    }

    /// <summary>获取所选选项的稳定编码。</summary>
    public string OptionCode { get; }
}

/// <summary>
/// 表示多选题所选择的一组稳定选项编码。
/// </summary>
public sealed class NutritionAssessmentMultipleChoiceAnswer : NutritionAssessmentAnswer
{
    /// <summary>使用非空的选项稳定编码集合建立回答。</summary>
    public NutritionAssessmentMultipleChoiceAnswer(IEnumerable<string> optionCodes)
    {
        ArgumentNullException.ThrowIfNull(optionCodes);
        var normalized = optionCodes
            .Select(code =>
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(code);
                return code;
            })
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (normalized.Length == 0)
        {
            throw new ArgumentException("多选回答至少需要一个选项。", nameof(optionCodes));
        }

        OptionCodes = Array.AsReadOnly(normalized);
    }

    /// <summary>获取按稳定顺序保存的所选选项编码。</summary>
    public IReadOnlyList<string> OptionCodes { get; }
}

/// <summary>
/// 表示数值题的十进制回答。
/// </summary>
public sealed class NutritionAssessmentDecimalAnswer : NutritionAssessmentAnswer
{
    /// <summary>使用十进制数值建立回答。</summary>
    public NutritionAssessmentDecimalAnswer(decimal value) => Value = value;

    /// <summary>获取回答数值。</summary>
    public decimal Value { get; }
}
