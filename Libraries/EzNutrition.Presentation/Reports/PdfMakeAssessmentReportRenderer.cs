using EzNutrition.Application.Reports;
using EzNutrition.Archives.Contracts.ValueObjects;
using Microsoft.JSInterop;

namespace EzNutrition.Presentation.Reports;

/// <summary>由两种 Blazor 宿主共用的本机 PDF 适配，不依赖具体浏览器或 Windows 打印接口。</summary>
public sealed class PdfMakeAssessmentReportRenderer(IJSRuntime js) : IAssessmentReportRenderer, IAsyncDisposable
{
    private IJSObjectReference? module;

    /// <inheritdoc />
    public CanonicalReference Template { get; } = new(
        new Uri("https://eznutrition.cdorey.net/report-templates/nutrition-assessment"), "1");

    /// <inheritdoc />
    public async ValueTask<byte[]> RenderAsync(AssessmentReportDraft draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.Report.PresentationTemplate != Template)
        {
            throw new InvalidOperationException("报告快照的模板版本与当前生成器不一致，请重新准备报告。");
        }

        module ??= await js.InvokeAsync<IJSObjectReference>("import", cancellationToken,
            "./_content/EzNutrition.Presentation/reports/assessment-report.mjs");
        var pdf = await module.InvokeAsync<byte[]>("render", cancellationToken, AssessmentReportPdfModel.From(draft));
        _ = ReportPdf.Identity(pdf);
        return pdf;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (module is not null)
        {
            try { await module.DisposeAsync(); }
            catch (JSDisconnectedException) { /* 宿主已关闭，无需再向已断开的 WebView 释放句柄。 */ }
        }
    }
}
