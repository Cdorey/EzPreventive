using System.IO.Compression;
using EzNutrition.Application.Archives;
using EzNutrition.Application.Consultations;
using EzNutrition.Application.Reports;
using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Archives.Contracts.Serialization;
using EzNutrition.Archives.Contracts.Validation;
using EzNutrition.Archives.Contracts.ValueObjects;
using EzNutrition.Archives.Xml;
using EzNutrition.Assessments.Common;
using EzNutrition.Domain.Consultations;

namespace EzNutrition.Client.Tests.Tests;

/// <summary>通过真实 XML codec 验证签发、保存、导出和原件重印的应用流程。</summary>
public sealed class ReportWorkflowTests
{
    /// <summary>签发和打印分别受控，拥有打印权限的人不必拥有签发权限。</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Independent_permissions_control_issue_and_evaluation(bool issue, bool print)
    {
        var h = new Harness();
        h.Access.Issue = issue;
        h.Access.Print = print;
        if (issue)
        {
            var preview = await h.Workflow.PrepareAsync(h.Workspace, h.Run, true);
            await h.Workflow.IssueAsync(preview);
            Assert.Single(h.Store.Documents);
        }
        else
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Workflow.PrepareAsync(h.Workspace, h.Run, true).AsTask());

        if (print)
        {
            var evaluation = await h.Workflow.PrepareAsync(h.Workspace, h.Run, false);
            await h.Workflow.PrintEvaluationAsync(evaluation);
            Assert.Single(h.Printer.Printed);
        }
        else
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Workflow.PrepareAsync(h.Workspace, h.Run, false).AsTask());
    }

    /// <summary>重复确认同一预览只保存一个版本，重印不重新渲染，也不受草稿修改影响。</summary>
    [Fact]
    public async Task Reprint_uses_saved_bytes_and_retry_does_not_issue_twice()
    {
        var h = new Harness();
        var preview = await h.Workflow.PrepareAsync(h.Workspace, h.Run, true);
        var id = await h.Workflow.IssueAsync(preview);
        Assert.Equal(id, await h.Workflow.IssueAsync(preview));
        Assert.Equal(1, h.Store.Saves);
        h.Run.SetAnswer("bmi-score", "below-18-5");
        h.Access.Issue = false;
        await h.Workflow.PrintStoredAsync(id);
        Assert.Equal(preview.Pdf.ToArray(), Assert.Single(h.Printer.Printed));
        Assert.Equal(1, h.Renderer.Calls);
        var read = await h.Workflow.ReadStoredAsync(id);
        Assert.Equal(0m, read.Document.Bundle.Entries.OfType<NutritionScaleAssessmentResource>().Single().TotalScore);
    }

    /// <summary>用户切换账号或失去权限后，旧预览不能继续签发。</summary>
    [Fact]
    public async Task Confirmation_rechecks_current_actor_and_permission()
    {
        var h = new Harness();
        var preview = await h.Workflow.PrepareAsync(h.Workspace, h.Run, true);
        h.Access.Actor = h.Access.Actor with { Display = "另一位医师" };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Workflow.IssueAsync(preview).AsTask());
        h.Access.Issue = false;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Workflow.IssueAsync(preview).AsTask());
        Assert.Empty(h.Store.Documents);
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Workflow.PrintEvaluationAsync(preview).AsTask());
    }

    /// <summary>保存失败不留下可见正式报告，并允许对同一预览重试。</summary>
    [Fact]
    public async Task Failed_save_can_retry_without_regenerating_pdf()
    {
        var h = new Harness();
        var preview = await h.Workflow.PrepareAsync(h.Workspace, h.Run, true);
        h.Store.FailSave = true;
        await Assert.ThrowsAsync<IOException>(() => h.Workflow.IssueAsync(preview).AsTask());
        Assert.Empty(h.Store.Documents);
        h.Store.FailSave = false;
        var id = await h.Workflow.IssueAsync(preview);
        Assert.Equal(preview.Draft.Report.Metadata.VersionId.Value, id);
        Assert.Equal(1, h.Renderer.Calls);
    }

    /// <summary>导入导出保留整个包，档案调阅能识别报告，但不会将它算作完整咨询历史。</summary>
    [Fact]
    public async Task Archive_browse_review_and_exchange_preserve_report_identity()
    {
        var h = new Harness();
        var preview = await h.Workflow.PrepareAsync(h.Workspace, h.Run, true);
        var id = await h.Workflow.IssueAsync(preview);
        var browse = await h.Archives.BrowseAsync();
        Assert.True(Assert.Single(browse.Records).IsReport);
        var opened = await h.Archives.OpenStoredAsync(id);
        Assert.True(opened.Operation.IsSuccess);
        Assert.NotNull(opened.Review);
        Assert.False((await h.Archives.ReadHistoryAsync(h.Workspace.ContractIdentity.Patient.ResourceId.Value, id)).Operation.IsSuccess);
        Assert.True((await h.Archives.ExportStoredAsync(id)).IsSuccess);
        var exported = Assert.IsType<ArchiveDocumentExport>(h.Transport.Exported);
        Assert.Equal(".ezreport", exported.Format.PreferredFileExtension);

        var other = new Harness();
        Assert.Equal(id, await other.Workflow.ImportAsync(new ExternalArchiveDocument { Content = exported.Content }));
        await other.Workflow.PrintStoredAsync(id);
        Assert.Equal(preview.Pdf.ToArray(), Assert.Single(other.Printer.Printed));
        Assert.Equal(0, other.Renderer.Calls);
        Assert.Equal(ArchiveOperationStatus.Denied, (await h.Archives.DeleteStoredAsync(id)).Status);
        Assert.Equal(ArchiveOperationStatus.Denied, (await h.Archives.ClearStoredAsync()).Status);
    }

    /// <summary>通用档案导出入口也检查报告打印权限，避免绕过专用按钮。</summary>
    [Fact]
    public async Task Generic_archive_export_cannot_bypass_print_permission()
    {
        var h = new Harness();
        var preview = await h.Workflow.PrepareAsync(h.Workspace, h.Run, true);
        var id = await h.Workflow.IssueAsync(preview);
        h.Access.Print = false;
        Assert.Equal(ArchiveOperationStatus.Denied, (await h.Archives.ExportStoredAsync(id)).Status);
        Assert.Null(h.Transport.Exported);
    }

    /// <summary>PDF 被替换或包中存在重复条目时，拒绝打印及导入。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Damaged_or_ambiguous_package_is_rejected(bool duplicate)
    {
        var h = new Harness();
        var preview = await h.Workflow.PrepareAsync(h.Workspace, h.Run, true);
        var id = await h.Workflow.IssueAsync(preview);
        var record = h.Store.Documents[id];
        using var buffer = new MemoryStream();
        buffer.Write(record.Content.Span);
        buffer.Position = 0;
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Update, leaveOpen: true))
        {
            if (!duplicate) zip.GetEntry("report.pdf")!.Delete();
            using var stream = zip.CreateEntry("report.pdf").Open();
            stream.Write("%PDF-1.7\nchanged"u8);
        }
        h.Store.Documents[id] = record with { Content = buffer.ToArray() };
        await Assert.ThrowsAsync<InvalidDataException>(() => h.Workflow.PrintStoredAsync(id).AsTask());
        Assert.Empty(h.Printer.Printed);
    }

    private sealed class Harness
    {
        public ConsultationWorkspace Workspace { get; } = new(new ClientInfo
        {
            Name = "模拟患者", Gender = "女", Age = new EzNutrition.Domain.Consultations.ChronologicalAge(70), Height = 165, Weight = 60
        });
        public NutritionAssessmentRun Run { get; }
        public Access Access { get; } = new();
        public Renderer Renderer { get; } = new();
        public Store Store { get; } = new();
        public Printer Printer { get; } = new();
        public Transport Transport { get; } = new();
        public ReportWorkflow Workflow { get; }
        public ArchiveWorkflow Archives { get; }

        public Harness()
        {
            var service = new NutritionAssessmentApplicationService([new MustInstrument()]);
            Run = service.StartRun(Workspace, service.Definitions.Single());
            Run.SetAnswer("bmi-score", "above-20");
            Run.SetAnswer("unplanned-weight-loss", "below-five-percent");
            Run.SetAnswer("acute-disease-effect", "absent");
            var assembler = new ArchiveContractAssembler(new ApplicationIdentity(new Uri("urn:test:report"), "报告测试", "1"));
            var validator = new ArchiveContractValidator();
            IArchiveCodec[] codecs = [new XmlArchiveCodec(validator)];
            Workflow = new(new(assembler), Renderer, Access, validator, new(codecs, validator), Store, Printer);
            Archives = new(assembler, validator, codecs, Store, Transport, Access);
        }
    }

    private sealed class Access : IReportAuthorization
    {
        public bool Issue { get; set; } = true;
        public bool Print { get; set; } = true;
        public ActorReference Actor { get; set; } = new()
        {
            Identifier = new BusinessIdentifier(new Uri("urn:test:users"), "doctor"), Display = "模拟医师"
        };
        public ValueTask<ActorReference> RequireIssuerAsync(CancellationToken cancellationToken = default) =>
            Issue ? ValueTask.FromResult(Actor) : throw new UnauthorizedAccessException();
        public ValueTask RequirePrintAsync(CancellationToken cancellationToken = default) =>
            Print ? ValueTask.CompletedTask : throw new UnauthorizedAccessException();
    }

    private sealed class Renderer : IAssessmentReportRenderer
    {
        public int Calls { get; private set; }
        public CanonicalReference Template { get; } = new(new Uri("urn:test:template"), "1");
        public ValueTask<byte[]> RenderAsync(AssessmentReportDraft draft, CancellationToken cancellationToken = default)
        {
            Calls++;
            return ValueTask.FromResult("%PDF-1.7\nfixture"u8.ToArray());
        }
    }

    private sealed class Printer : IReportPrinter
    {
        public List<byte[]> Printed { get; } = [];
        public ValueTask PrintAsync(ReadOnlyMemory<byte> pdf, string title, CancellationToken cancellationToken = default)
        {
            Printed.Add(pdf.ToArray());
            return ValueTask.CompletedTask;
        }
    }

    private sealed class Store : IArchiveDocumentStore
    {
        public Dictionary<Guid, StoredArchiveDocument> Documents { get; } = [];
        public int Saves { get; private set; }
        public bool FailSave { get; set; }
        public ArchiveDocumentStoreCapabilities Capabilities => ArchiveDocumentStoreCapabilities.Save
            | ArchiveDocumentStoreCapabilities.Browse | ArchiveDocumentStoreCapabilities.Delete | ArchiveDocumentStoreCapabilities.Clear
            | ArchiveDocumentStoreCapabilities.CompareExchange;
        public async ValueTask<bool> CompareExchangeAsync(StoredArchiveDocument document, ReadOnlyMemory<byte>? expectedContent,
            CancellationToken cancellationToken = default)
        {
            var existing = Documents.GetValueOrDefault(document.Info.DocumentId);
            if (expectedContent is null ? existing is not null
                : existing is null || !existing.Content.Span.SequenceEqual(expectedContent.Value.Span)) return false;
            await SaveAsync(document, cancellationToken);
            return true;
        }
        public ValueTask SaveAsync(StoredArchiveDocument document, CancellationToken cancellationToken = default)
        {
            if (FailSave) throw new IOException("模拟存储故障");
            Saves++;
            Documents[document.Info.DocumentId] = document;
            return ValueTask.CompletedTask;
        }
        public ValueTask<IReadOnlyList<StoredArchiveDocumentInfo>> ListAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<StoredArchiveDocumentInfo>>(Documents.Values.Select(doc => doc.Info).ToArray());
        public ValueTask<StoredArchiveDocument?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Documents.GetValueOrDefault(id));
        public ValueTask DeleteAsync(Guid id, CancellationToken cancellationToken = default) { Documents.Remove(id); return ValueTask.CompletedTask; }
        public ValueTask ClearAsync(CancellationToken cancellationToken = default) { Documents.Clear(); return ValueTask.CompletedTask; }
    }

    private sealed class Transport : IArchiveDocumentTransport
    {
        public ArchiveDocumentExport? Exported { get; private set; }
        public bool CanOpen => true;
        public bool CanSave => true;
        public ValueTask<ExternalArchiveDocument?> OpenAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult<ExternalArchiveDocument?>(null);
        public ValueTask<bool> SaveAsync(ArchiveDocumentExport document, CancellationToken cancellationToken = default)
        {
            Exported = document;
            return ValueTask.FromResult(true);
        }
    }
}
