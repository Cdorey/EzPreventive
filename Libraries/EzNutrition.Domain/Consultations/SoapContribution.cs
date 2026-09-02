namespace EzNutrition.Domain.Consultations;

/// <summary>
/// 表示一个领域对象对 SOAP 记录的不可变候选贡献。
/// </summary>
/// <param name="Subjective">由咨询对象报告或表达的主观资料。</param>
/// <param name="Objective">由测量、计算或评估产生的客观资料。</param>
/// <param name="Assessment">由专业判断形成的问题评估候选文本；为 <see langword="null"/> 时表示当前对象不提供该部分。</param>
/// <param name="Plan">由专业决策形成的处理计划候选文本；为 <see langword="null"/> 时表示当前对象不提供该部分。</param>
public sealed record SoapContribution(
    string Subjective = "",
    string Objective = "",
    string? Assessment = null,
    string? Plan = null);
