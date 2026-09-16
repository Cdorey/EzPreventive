using EzNutrition.Application.Reports;
using EzNutrition.Application.Consultations;
using EzNutrition.Archives.Contracts.ValueObjects;
using Microsoft.JSInterop;

namespace EzNutrition.Presentation.Reports;

/// <summary>由两种 Blazor 宿主共用的本机 PDF 适配，不依赖具体浏览器或 Windows 打印接口。</summary>
public sealed class PdfMakeAssessmentReportRenderer(IJSRuntime js) : IAssessmentReportRenderer
{
    /// <inheritdoc />
    public CanonicalReference Template { get; } = new(
        new Uri("https://eznutrition.cdorey.net/report-templates/nutrition-assessment"), "3");

    /// <inheritdoc />
    public async ValueTask<byte[]> RenderAsync(AssessmentReportDraft draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.Report.PresentationTemplate != Template)
        {
            throw new InvalidOperationException("报告快照的模板版本与当前生成器不一致，请重新准备报告。");
        }

        return await RenderModelAsync(AssessmentReportPdfModel.From(draft), cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<byte[]> RenderEvaluationAsync(NutritionAssessmentSnapshot snapshot, DateTimeOffset generatedAt,
        CancellationToken cancellationToken = default) => RenderModelAsync(AssessmentReportPdfModel.From(snapshot, generatedAt), cancellationToken);

    private async ValueTask<byte[]> RenderModelAsync(AssessmentReportPdfModel model, CancellationToken cancellationToken)
    {
        // 浏览器会缓存模块；句柄只在本次操作中持有，避免 WebView 关闭后再等待 JS 释放。
        await using var module = await js.InvokeAsync<IJSObjectReference>("import", cancellationToken,
            "./_content/EzNutrition.Presentation/reports/assessment-report.mjs");
        var pdf = await module.InvokeAsync<byte[]>("render", cancellationToken, model);
        _ = ReportPdf.Identity(pdf);
        return pdf;
    }
}
