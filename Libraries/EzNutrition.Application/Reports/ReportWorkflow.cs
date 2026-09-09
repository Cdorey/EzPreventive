using EzNutrition.Application.Archives;
using EzNutrition.Application.Consultations;
using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Archives.Contracts.Validation;
using EzNutrition.Archives.Contracts.ValueObjects;

namespace EzNutrition.Application.Reports;

/// <summary>由展示层适配现有认证状态与 policy，不创建另一套授权规则。</summary>
public interface IReportAuthorization
{
    /// <summary>要求签发权限并取得当前签发人的身份；不允许时抛出授权异常。</summary>
    ValueTask<ActorReference> RequireIssuerAsync(CancellationToken cancellationToken = default);

    /// <summary>要求打印权限；不允许时抛出授权异常。</summary>
    ValueTask RequirePrintAsync(CancellationToken cancellationToken = default);
}

/// <summary>将已生成的 PDF 交给宿主打印界面，不声称纸张已经输出。</summary>
public interface IReportPrinter
{
    /// <summary>打开 PDF 的打印交互；文件必须是调用方已核对的原件或明确的评估稿。</summary>
    ValueTask PrintAsync(ReadOnlyMemory<byte> pdf, string title, CancellationToken cancellationToken = default);
}

/// <summary>将一次审核预览与其确切 PDF 绑定；确认时复用这些字节，不再次生成。</summary>
public sealed class PreparedAssessmentReport
{
    internal PreparedAssessmentReport(AssessmentReportDraft draft, byte[] pdf)
    {
        Draft = draft;
        Pdf = pdf;
    }

    /// <summary>获取捕获的数据与拟签发信息。</summary>
    public AssessmentReportDraft Draft { get; }

    /// <summary>获取实际审核的 PDF 字节。</summary>
    public ReadOnlyMemory<byte> Pdf { get; }
}

/// <summary>编排本机报告预览、签发保存和原件打印；临床内容不会进入后端接口。</summary>
public sealed class ReportWorkflow(
    AssessmentReportFactory factory,
    IAssessmentReportRenderer renderer,
    IReportAuthorization authorization,
    IArchiveValidator validator,
    ReportPackage package,
    IArchiveDocumentStore store,
    IReportPrinter printer)
{
    /// <summary>生成待审核的正式成品或带水印评估稿，未确认前不写入档案库。</summary>
    public async ValueTask<PreparedAssessmentReport> PrepareAsync(
        ConsultationWorkspace workspace,
        NutritionAssessmentRun assessment,
        bool forIssuance,
        CancellationToken cancellationToken = default)
    {
        ActorReference? signer = null;
        if (forIssuance)
        {
            RequireStorage();
            signer = await authorization.RequireIssuerAsync(cancellationToken);
        }
        else
        {
            await authorization.RequirePrintAsync(cancellationToken);
        }

        var draft = factory.Create(workspace, assessment, renderer.Template, signer, DateTimeOffset.UtcNow);
        var pdf = await renderer.RenderAsync(draft, cancellationToken);
        _ = ReportPdf.Identity(pdf);
        return new PreparedAssessmentReport(draft, pdf);
    }

    /// <summary>
    /// 用户确认预览后保存报告包。重复确认同一预览复用文档标识；保存成功才算签发完成。
    /// </summary>
    public async ValueTask<Guid> IssueAsync(PreparedAssessmentReport prepared, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        RequireStorage();
        var signer = await authorization.RequireIssuerAsync(cancellationToken);
        if (prepared.Draft.Signer != signer)
            throw new UnauthorizedAccessException("签发人已变化，请重新准备和审核报告。");

        var signed = new SignedReport
        {
            Document = prepared.Draft.BindSignedPdf(prepared.Pdf.Span, validator),
            Pdf = prepared.Pdf
        };
        var bytes = await package.WriteAsync(signed, cancellationToken);
        var record = CreateStoredDocument(signed, bytes);
        await SaveNewAsync(record, cancellationToken);
        return record.Info.DocumentId;
    }

    /// <summary>打印评估稿；未保存的正式预览不能从此入口当作已签发原件输出。</summary>
    public async ValueTask PrintEvaluationAsync(PreparedAssessmentReport prepared, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        await authorization.RequirePrintAsync(cancellationToken);
        if (prepared.Draft.Signer is not null)
            throw new InvalidOperationException("正式报告请先确认签发，再读取已保存原件打印。");
        await printer.PrintAsync(prepared.Pdf, prepared.Draft.Report.Title ?? "量表评估稿", cancellationToken);
    }

    /// <summary>从本机档案库核对并读取原件；不读取工作区或最新模板。</summary>
    public async ValueTask<SignedReport> ReadStoredAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var stored = await store.GetAsync(documentId, cancellationToken)
            ?? throw new FileNotFoundException("未找到指定报告，可能已被移除。");
        if (stored.Info.FormatIdentifier != ReportPackage.Format.Identifier.AbsoluteUri)
            throw new InvalidDataException("所选档案不是报告包。");
        var report = await package.ReadAsync(stored.Content, cancellationToken);
        if (report.Report.Metadata.VersionId.Value != documentId)
            throw new InvalidDataException("报告索引与原件身份不一致。");
        return report;
    }

    /// <summary>只要求打印权限，允许其他具有打印权限的使用者输出医师签发的原件。</summary>
    public async ValueTask PrintStoredAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        await authorization.RequirePrintAsync(cancellationToken);
        var report = await ReadStoredAsync(documentId, cancellationToken);
        await printer.PrintAsync(report.Pdf, report.Report.Title ?? "营养报告", cancellationToken);
    }

    /// <summary>将用户选择的完整报告包存入本机档案库；不重签发、不重新编码或改写原件。</summary>
    public async ValueTask<Guid> ImportAsync(ExternalArchiveDocument external, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(external);
        RequireStorage();
        var report = await package.ReadAsync(external.Content, cancellationToken);
        var document = CreateStoredDocument(report, external.Content);
        await SaveNewAsync(document, cancellationToken);
        return document.Info.DocumentId;
    }

    /// <summary>以不存在为条件原子新增；同一成品重试可复用结果，其他内容不能覆盖原件。</summary>
    private async ValueTask SaveNewAsync(StoredArchiveDocument document, CancellationToken cancellationToken)
    {
        if (await store.CompareExchangeAsync(document, expectedContent: null, cancellationToken)) return;
        var existing = await store.GetAsync(document.Info.DocumentId, cancellationToken);
        if (existing is null || !existing.Content.Span.SequenceEqual(document.Content.Span))
            throw new InvalidDataException("本机已存在不同内容或档案已变化，未覆盖既有报告包，请重新读取后核对。");
    }

    private void RequireStorage()
    {
        const ArchiveDocumentStoreCapabilities required = ArchiveDocumentStoreCapabilities.Save
            | ArchiveDocumentStoreCapabilities.Browse | ArchiveDocumentStoreCapabilities.CompareExchange;
        if ((store.Capabilities & required) != required)
            throw new InvalidOperationException("当前宿主不支持正式报告保存和原件读取。");
    }

    internal static StoredArchiveDocument CreateStoredDocument(SignedReport signed, ReadOnlyMemory<byte> content)
    {
        var consultation = signed.Document.Bundle.Entries.OfType<ConsultationResource>().Single();
        var report = signed.Report;
        return new StoredArchiveDocument
        {
            Info = new StoredArchiveDocumentInfo
            {
                DocumentId = report.Metadata.VersionId.Value,
                PatientId = report.SubjectReference.ResourceId.Value,
                Title = report.Title ?? "营养报告",
                SubjectDisplay = consultation.SubjectSnapshot?.IdentityDisplay ?? "未提供",
                ConsultationStartedAt = consultation.Period.Start,
                LastSavedAt = report.Metadata.FinalizedAt!.Value,
                FormatIdentifier = ReportPackage.Format.Identifier.AbsoluteUri,
                FormatVersion = ReportPackage.Format.Version,
                MediaType = ReportPackage.Format.MediaType!,
                FormatDisplayName = ReportPackage.Format.DisplayName,
                PreferredFileExtension = ReportPackage.Format.PreferredFileExtension
            },
            Content = content
        };
    }
}
