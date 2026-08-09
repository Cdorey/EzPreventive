using EzNutrition.Archives.Contracts.Bundles;
using EzNutrition.Archives.Contracts.Serialization;

namespace EzNutrition.Archives.Contracts.Tests.Fixtures;

/// <summary>
/// 表示一个具有稳定名称和用途说明的完全虚构档案样本。
/// </summary>
/// <param name="Key">供测试定位样本的稳定键。</param>
/// <param name="Description">样本所覆盖的业务情境。</param>
/// <param name="Document">样本包含的档案文档。</param>
internal sealed record ArchiveSample(string Key, string Description, ArchiveDocument Document)
{
    /// <summary>
    /// 获取样本包含的档案资源包。
    /// </summary>
    public ArchiveBundle Bundle => Document.Bundle;
}
