namespace EzNutrition.Domain.Consultations;

/// <summary>
/// 表示能够根据自身当前领域状态生成 SOAP 资料贡献的对象。
/// </summary>
public interface ISoapContributor
{
    /// <summary>
    /// 创建当前对象对 SOAP 记录的确定性候选贡献。
    /// </summary>
    /// <returns>当前对象能够生成的 SOAP 候选文本；不适用的部分可为 <see langword="null"/>。</returns>
    SoapContribution ToSoapContribution();
}
