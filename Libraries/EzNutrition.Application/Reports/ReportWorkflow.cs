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
    internal PreparedAssessmentReport(AssessmentReportDraft draft, byte[] pdf, ReadOnlyMemory<byte>? expectedContent = null)
    {
        Draft = draft;
        Pdf = pdf;
        ExpectedContent = expectedContent;
    }

    /// <summary>获取捕获的数据与拟签发信息。</summary>
    public AssessmentReportDraft Draft { get; }

    /// <summary>获取实际审核的 PDF 字节。</summary>
    public ReadOnlyMemory<byte> Pdf { get; }

    /// <summary>保存审核时读取的旧报告包，提交时原子比较；不另维护当前版本指针。</summary>
    internal ReadOnlyMemory<byte>? ExpectedContent { get; }
}

/// <summary>表示同一量表可以明确选择更正的一份报告，不按时间戳自动选取。</summary>
/// <param name="DocumentId">本机报告档案标识。</param>
/// <param name="ReportId">打印在原件上的逻辑报告编号。</param>
/// <param name="RevisionNumber">当前修订号。</param>
/// <param name="FinalizedAt">当前版本签发时间，仅用于展示。</param>
public sealed record ReportRevisionCandidate(Guid DocumentId, Guid ReportId, int RevisionNumber, DateTimeOffset FinalizedAt);

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

    /// <summary>准备同一患者、咨询及量表的报告更正；不会立即改变旧报告的有效状态。</summary>
    public async ValueTask<PreparedAssessmentReport> PrepareRevisionAsync(
        ConsultationWorkspace workspace, NutritionAssessmentRun assessment, Guid documentId,
        CancellationToken cancellationToken = default)
    {
        RequireStorage();
        var signer = await authorization.RequireIssuerAsync(cancellationToken);
        var stored = await store.GetAsync(documentId, cancellationToken)
            ?? throw new FileNotFoundException("未找到要更正的报告。");
        var previous = await DecodeStoredAsync(stored, cancellationToken);
        var draft = factory.Create(workspace, assessment, renderer.Template, signer, DateTimeOffset.UtcNow, previous);
        var pdf = await renderer.RenderAsync(draft, cancellationToken);
        _ = ReportPdf.Identity(pdf);
        return new PreparedAssessmentReport(draft, pdf, stored.Content);
    }

    /// <summary>列出当前量表的既有报告，供用户明确选择更正对象；不同评估实例不互相替代。</summary>
    public async ValueTask<IReadOnlyList<ReportRevisionCandidate>> ListRevisableAsync(
        ConsultationWorkspace workspace, NutritionAssessmentRun assessment, CancellationToken cancellationToken = default)
    {
        var result = new List<ReportRevisionCandidate>();
        foreach (var info in await store.ListAsync(cancellationToken))
        {
            if (info.FormatIdentifier != ReportPackage.Format.Identifier.AbsoluteUri
                || info.PatientId != workspace.ContractIdentity.Patient.ResourceId.Value) continue;
            var stored = await ReadStoredAsync(info.DocumentId, cancellationToken);
            if (stored.Report.ConsultationReference.ResourceId == workspace.ContractIdentity.Consultation.ResourceId
                && stored.Report.InputResourceReferences.Any(reference => reference.ResourceId == assessment.ArchiveIdentity.ResourceId))
                result.Add(new ReportRevisionCandidate(info.DocumentId, stored.Report.Metadata.ResourceId.Value, stored.Report.Metadata.RevisionNumber.Value,
                    stored.Report.Metadata.FinalizedAt!.Value));
        }
        return result.OrderByDescending(candidate => candidate.FinalizedAt).ToArray();
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

        var previousPdfs = prepared.Draft.Previous?.PreviousPdfs.ToDictionary(pair => pair.Key, pair => pair.Value)
            ?? new Dictionary<Guid, ReadOnlyMemory<byte>>();
        if (prepared.Draft.Previous is { } previous)
            previousPdfs.Add(previous.Report.Metadata.VersionId.Value, previous.Pdf);
        var signed = new SignedReport
        {
            Document = prepared.Draft.BindSignedPdf(prepared.Pdf.Span, validator),
            Pdf = prepared.Pdf,
            PreviousPdfs = previousPdfs
        };
        var bytes = await package.WriteAsync(signed, cancellationToken);
        var record = CreateStoredDocument(signed, bytes);
        await CommitAsync(record, prepared.ExpectedContent, cancellationToken);
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
        return await DecodeStoredAsync(stored, cancellationToken);
    }

    private async ValueTask<SignedReport> DecodeStoredAsync(StoredArchiveDocument stored, CancellationToken cancellationToken)
    {
        if (stored.Info.FormatIdentifier != ReportPackage.Format.Identifier.AbsoluteUri)
            throw new InvalidDataException("所选档案不是报告包。");
        var report = await package.ReadAsync(stored.Content, cancellationToken);
        if (report.DocumentId != stored.Info.DocumentId)
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
        var existing = await store.GetAsync(document.Info.DocumentId, cancellationToken);
        if (existing is not null)
        {
            if (existing.Content.Span.SequenceEqual(external.Content.Span)) return document.Info.DocumentId;
            var previous = await DecodeStoredAsync(existing, cancellationToken);
            if (!await package.PreservesHistoryAsync(report, previous, cancellationToken))
                throw new InvalidDataException("导入报告与本机历史不一致或版本较旧，未合并冲突，也未覆盖原件。");
            if (report.Versions.Count == previous.Versions.Count) return document.Info.DocumentId;
        }
        await CommitAsync(document, existing?.Content, cancellationToken);
        return document.Info.DocumentId;
    }

    /// <summary>根据审核时的正文原子提交；同一成品重试可复用结果，冲突需要重新读取审核。</summary>
    private async ValueTask CommitAsync(StoredArchiveDocument document, ReadOnlyMemory<byte>? expectedContent,
        CancellationToken cancellationToken)
    {
        if (await store.CompareExchangeAsync(document, expectedContent, cancellationToken)) return;
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
        var consultation = signed.Consultation;
        var report = signed.Report;
        return new StoredArchiveDocument
        {
            Info = new StoredArchiveDocumentInfo
            {
                DocumentId = signed.DocumentId,
                PatientId = report.SubjectReference.ResourceId.Value,
                Title = report.Title ?? "营养报告",
                SubjectDisplay = consultation.SubjectSnapshot?.IdentityDisplay ?? "未提供",
                ConsultationStartedAt = consultation.Period.Start,
                LastSavedAt = report.Metadata.FinalizedAt!.Value,
                FormatIdentifier = ReportPackage.Format.Identifier.AbsoluteUri,
                FormatVersion = signed.SourcePackageVersion ?? ReportPackage.Format.Version,
                MediaType = ReportPackage.Format.MediaType!,
                FormatDisplayName = ReportPackage.Format.DisplayName,
                PreferredFileExtension = ReportPackage.Format.PreferredFileExtension
            },
            Content = content
        };
    }
}
