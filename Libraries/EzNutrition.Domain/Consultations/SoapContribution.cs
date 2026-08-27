namespace EzNutrition.Domain.Consultations;

/// <summary>
/// 表示一个领域对象对 SOAP 主观资料和客观资料的不可变贡献。
/// </summary>
/// <param name="Subjective">由咨询对象报告或表达的主观资料。</param>
/// <param name="Objective">由测量、计算或评估产生的客观资料。</param>
public sealed record SoapContribution(
    string Subjective = "",
    string Objective = "");
