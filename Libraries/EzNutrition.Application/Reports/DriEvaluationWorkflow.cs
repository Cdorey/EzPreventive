namespace EzNutrition.Application.Reports;

/// <summary>将已捕获的 DRIs 结果生成为本机评估稿，始终保留未经审核的水印。</summary>
public interface IDriEvaluationRenderer
{
    /// <summary>只消费传入的快照，不读取页面或获取新的参考数据。</summary>
    ValueTask<byte[]> RenderAsync(DriEvaluationSnapshot snapshot, CancellationToken cancellationToken = default);
}

/// <summary>输出 DRIs 独立速查结果，不依赖档案存储或患者咨询。</summary>
public sealed class DriEvaluationWorkflow(IReportAuthorization authorization, IDriEvaluationRenderer renderer, IReportPrinter printer)
{
    /// <summary>检查当前打印权限，生成评估稿并交给宿主查看及打印。</summary>
    public async ValueTask PrintAsync(DriEvaluationSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        await authorization.RequirePrintAsync(cancellationToken);
        var pdf = await renderer.RenderAsync(snapshot, cancellationToken);
        _ = ReportPdf.Identity(pdf);
        await authorization.RequirePrintAsync(cancellationToken);
        await printer.PrintAsync(pdf, "DRIs 速查 · 评估稿", cancellationToken);
    }
}
