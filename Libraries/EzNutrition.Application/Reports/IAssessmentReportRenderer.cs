using EzNutrition.Archives.Contracts.ValueObjects;

namespace EzNutrition.Application.Reports;

/// <summary>将已捕获的量表报告快照生成为本机 PDF，不读取当前工作区或调用远程计算。</summary>
public interface IAssessmentReportRenderer
{
    /// <summary>获取本实现采用的模板标识及确切版本。</summary>
    CanonicalReference Template { get; }

    /// <summary>生成供预览、签发绑定或评估输出使用的完整 PDF 字节。</summary>
    ValueTask<byte[]> RenderAsync(AssessmentReportDraft draft, CancellationToken cancellationToken = default);
}
