namespace EzNutrition.Domain.Consultations;

/// <summary>
/// 表示能够根据自身当前领域状态生成 SOAP 资料贡献的对象。
/// </summary>
public interface ISoapContributor
{
    /// <summary>
    /// 创建当前对象对 SOAP 主观资料和客观资料的确定性贡献。
    /// </summary>
    /// <returns>不包含专业评估和处理计划的 SOAP 资料贡献。</returns>
    SoapContribution ToSoapContribution();
}
