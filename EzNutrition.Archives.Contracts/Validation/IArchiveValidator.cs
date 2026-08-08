using EzNutrition.Archives.Contracts.Abstractions;
using EzNutrition.Archives.Contracts.Bundles;

namespace EzNutrition.Archives.Contracts.Validation;

/// <summary>
/// 定义格式无关的档案结构、完整性和临床提示校验。
/// </summary>
public interface IArchiveValidator
{
    /// <summary>
    /// 校验一个独立档案资源。
    /// </summary>
    /// <param name="resource">待校验资源。</param>
    /// <param name="scope">当前业务边界。</param>
    /// <returns>结构化校验结果。</returns>
    ArchiveValidationResult ValidateResource(IArchiveResource resource, ArchiveValidationScope scope);

    /// <summary>
    /// 校验 Bundle 及其资源引用闭包。
    /// </summary>
    /// <param name="bundle">待校验 Bundle。</param>
    /// <param name="scope">当前业务边界。</param>
    /// <returns>结构化校验结果。</returns>
    ArchiveValidationResult ValidateBundle(ArchiveBundle bundle, ArchiveValidationScope scope);
}
