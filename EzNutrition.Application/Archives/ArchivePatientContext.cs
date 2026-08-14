using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Archives.Contracts.ValueObjects;

namespace EzNutrition.Application.Archives;

/// <summary>
/// 表示从既有档案恢复的患者身份和最近一次咨询快照，用于开始新的独立咨询。
/// </summary>
public sealed class ArchivePatientContext
{
    internal ArchivePatientContext(PatientResource patient, SubjectSnapshot? snapshot)
    {
        SourcePatient = patient;
        PatientId = patient.Metadata.ResourceId.Value;
        Name = patient.Names
            .Select(name => name.Text)
            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))?
            .Trim();
        Name ??= NormalizeOptional(snapshot?.IdentityDisplay);
        Gender = FormatSupportedSex(snapshot?.AdministrativeSex ?? patient.AdministrativeSex);
        AgeInYears = WholeNumber(snapshot?.AgeAtConsultation, "a");
        HeightInCentimeters = UcumValue(snapshot?.Height?.Value, "cm");
        WeightInKilograms = UcumValue(snapshot?.Weight?.Value, "kg");
        PhysiologicalState = FormatSupportedPhysiologicalState(snapshot?.PhysiologicalStates);
    }

    internal PatientResource SourcePatient { get; }

    /// <summary>获取跨咨询保持稳定的患者逻辑标识。</summary>
    public Guid PatientId { get; }

    /// <summary>获取可用于新咨询表单的患者显示姓名。</summary>
    public string? Name { get; }

    /// <summary>获取当前工作区能够识别的性别显示值。</summary>
    public string? Gender { get; }

    /// <summary>获取最近一次咨询采用的整岁年龄。</summary>
    public int? AgeInYears { get; }

    /// <summary>获取最近一次咨询采用的厘米身高。</summary>
    public decimal? HeightInCentimeters { get; }

    /// <summary>获取最近一次咨询采用的千克体重。</summary>
    public decimal? WeightInKilograms { get; }

    /// <summary>获取当前工作区能够识别的最近一次生理状态。</summary>
    public string? PhysiologicalState { get; }

    private static int? WholeNumber(Quantity? quantity, string unitCode)
    {
        var value = UcumValue(quantity, unitCode);
        return value is >= int.MinValue and <= int.MaxValue && decimal.Truncate(value.Value) == value.Value
            ? decimal.ToInt32(value.Value)
            : null;
    }

    private static decimal? UcumValue(Quantity? quantity, string unitCode) =>
        quantity is { Comparator: QuantityComparator.None } &&
        quantity.Unit.System.AbsoluteUri == "http://unitsofmeasure.org/" &&
        string.Equals(quantity.Unit.Code, unitCode, StringComparison.Ordinal)
            ? quantity.Value
            : null;

    private static string? FormatSupportedSex(Coding? sex) => sex?.Code switch
    {
        "male" => "男",
        "female" => "女",
        _ => null
    };

    private static string? FormatSupportedPhysiologicalState(IEnumerable<Coding>? states)
    {
        foreach (var state in states ?? [])
        {
            var value = state.Code switch
            {
                "pregnancy-first-trimester" => "孕早期",
                "pregnancy-second-trimester" => "孕中期",
                "pregnancy-third-trimester" => "孕晚期",
                "lactation" => "乳母",
                "postmenopausal" => "已绝经",
                _ => null
            };
            if (value is not null)
            {
                return value;
            }
        }

        return null;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
