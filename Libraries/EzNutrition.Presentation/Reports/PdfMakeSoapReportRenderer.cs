using System.Globalization;
using EzNutrition.Application.Reports;
using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Archives.Contracts.ValueObjects;
using Microsoft.JSInterop;

namespace EzNutrition.Presentation.Reports;

/// <summary>将 SOAP 固定快照排版为本机 PDF。</summary>
public sealed class PdfMakeSoapReportRenderer(IJSRuntime js) : ISoapReportRenderer
{
    /// <inheritdoc />
    public CanonicalReference Template { get; } = new(new Uri("https://eznutrition.cdorey.net/report-templates/soap-note"), "1");

    /// <inheritdoc />
    public async ValueTask<byte[]> RenderAsync(SoapReportDraft draft, CancellationToken cancellationToken = default)
    {
        if (draft.Report.PresentationTemplate != Template)
            throw new InvalidOperationException("SOAP 报告模板版本已变化，请重新生成预览。");
        await using var module = await js.InvokeAsync<IJSObjectReference>("import", cancellationToken,
            "./_content/EzNutrition.Presentation/reports/soap-report.mjs");
        var pdf = await module.InvokeAsync<byte[]>("render", cancellationToken, SoapReportPdfModel.From(draft));
        _ = ReportPdf.Identity(pdf);
        return pdf;
    }
}

/// <summary>保存一节 SOAP 原文；空节保留标题和空白正文。</summary>
internal sealed record SoapReportSection(string Title, string Text);

/// <summary>提供固定患者信息和 SOAP 四节纯文本。</summary>
internal sealed record SoapReportPdfModel(
    string Title, string ReportNumber, int RevisionNumber, string Patient, string Subject,
    string ReportTime, string Signer, string Institution, bool IsEvaluation, IReadOnlyList<SoapReportSection> Sections)
{
    public static SoapReportPdfModel From(SoapReportDraft draft)
    {
        var note = draft.Document.Bundle.Entries.OfType<SoapNoteResource>().Single();
        var subject = draft.Document.Bundle.Entries.OfType<ConsultationResource>().Single().SubjectSnapshot!;
        return new(draft.Report.Title!, draft.Report.Metadata.ResourceId.Value.ToString("D"),
            draft.Report.Metadata.RevisionNumber.Value, subject.IdentityDisplay ?? "未关联患者",
            $"{subject.AdministrativeSex?.Display ?? "性别未提供"} · {subject.ChronologicalAgeAtConsultation?.ToString() ?? "年龄未提供"}"
                + $" · 身高 {Quantity(subject.Height?.Value)} · 体重 {Quantity(subject.Weight?.Value)}"
                + (subject.PhysiologicalStates.Count > 0 ? " · " + string.Join("、", subject.PhysiologicalStates.Select(value => value.Display ?? value.Code)) : ""),
            draft.Report.Metadata.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
            draft.Signer?.Display ?? "未经医师审核签发", draft.Signer?.Organization?.Display ?? "", draft.Signer is null,
            [Section("S · 主观资料", note.Subjective), Section("O · 客观资料", note.Objective),
                Section("A · 问题评估", note.Assessment), Section("P · 处理计划", note.Plan)]);
    }

    private static SoapReportSection Section(string title, string? text) => new(title, string.IsNullOrWhiteSpace(text) ? "" : text);
    private static string Quantity(Quantity? value) => value is null ? "未记录"
        : value.Value.ToString("0.##", CultureInfo.InvariantCulture) + " " + (value.Unit.Display ?? value.Unit.Code);
}
