using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using EzNutrition.Archives.Contracts.Metadata;
using EzNutrition.Archives.Contracts.Identity;
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
public sealed partial class ReportWorkflowTests
{
    /// <summary>验证只有打印权限也可输出快照，后续作答不进入成品且不触发归档。</summary>
    [Fact]
    public async Task Standalone_print_captures_answers_without_archiving_or_requiring_issuance()
    {
        var h = new Harness();
        h.Access.Issue = false;
        h.Renderer.AfterEvaluation = () => h.Run.SetAnswer("bmi-score", "below-18-5");

        await h.Workflow.PrintStandaloneAsync(h.Run);

        Assert.Equal(0m, h.Renderer.Evaluation!.TotalScore);
        Assert.Equal(2m, h.Run.Evaluation.TotalScore);
        Assert.Equal("above-20", ((CodingArchiveValue)h.Renderer.Evaluation.Responses[0].Answer!).Value.Code.Split('/').Last());
        Assert.Single(h.Printer.Printed);
        Assert.Empty(h.Store.Documents);
        Assert.Equal(0, h.Store.Saves);
    }

    /// <summary>验证未完成项目保留缺失标记，生成期间失去打印权限时不打开输出窗口。</summary>
    [Fact]
    public async Task Standalone_print_preserves_unanswered_items_and_checks_current_permission()
    {
        var h = new Harness();
        h.Run.ClearAnswer("bmi-score");
        h.Renderer.AfterEvaluation = () => h.Access.Print = false;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Workflow.PrintStandaloneAsync(h.Run).AsTask());

        Assert.Null(h.Renderer.Evaluation!.TotalScore);
        Assert.Equal(DataAbsentReasonCode.NotEstablished, h.Renderer.Evaluation.TotalScoreAbsentReason);
        Assert.Equal(3, h.Renderer.Evaluation.Responses.Count);
        Assert.Equal(DataAbsentReasonCode.NotAsked, h.Renderer.Evaluation.Responses[0].AnswerAbsentReason);
        Assert.Empty(h.Printer.Printed);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Workflow.PrintStandaloneAsync(h.Run).AsTask());
        Assert.Equal(1, h.Renderer.Calls);
        Assert.Empty(h.Store.Documents);
    }

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

    /// <summary>更正只在提交后替代旧版，保留旧资源和 PDF，并从同一档案入口重印新版。</summary>
    [Fact]
    public async Task Revision_keeps_old_original_and_commits_a_closed_version_chain()
    {
        var h = new Harness();
        var initial = await h.Workflow.PrepareAsync(h.Workspace, h.Run, true);
        var id = await h.Workflow.IssueAsync(initial);
        var original = await h.Workflow.ReadStoredAsync(id);
        h.Run.SetAnswer("bmi-score", "below-18-5");
        var correction = await h.Workflow.PrepareRevisionAsync(h.Workspace, h.Run, id);
        Assert.Equal(original.Report.Metadata.VersionId, correction.Draft.Report.Metadata.BasedOn!.VersionId);
        Assert.Null(correction.Draft.Report.Metadata.Supersedes);
        Assert.Single((await h.Workflow.ReadStoredAsync(id)).Versions);
        Assert.Equal(id, await h.Workflow.IssueAsync(correction));
        Assert.Equal(id, await h.Workflow.IssueAsync(correction));
        var revised = await h.Workflow.ReadStoredAsync(id);
        Assert.Equal(2, revised.Versions.Count);
        Assert.Equal(original.Report.Metadata.ResourceId, revised.Report.Metadata.ResourceId);
        Assert.Equal(original.Report.Metadata.VersionId, revised.Report.Metadata.Supersedes!.VersionId);
        Assert.Equal(ResourceLifecycleStatus.Final, revised.Versions[0].Metadata.Status);
        Assert.Equal(ResourceLifecycleStatus.Amended, revised.Report.Metadata.Status);
        Assert.Equal(original.Pdf.ToArray(), revised.PreviousPdfs[original.Report.Metadata.VersionId.Value].ToArray());
        Assert.Equal(correction.Pdf.ToArray(), revised.Pdf.ToArray());
        Assert.Single(h.Store.Documents);
        Assert.Equal(2, h.Store.Saves);
        var opened = await h.Archives.OpenStoredAsync(id);
        Assert.True(opened.Operation.IsSuccess);
        Assert.Contains(opened.Review!.Sections, section => section.Description?.Contains("已被后续") == true);
        h.Access.Issue = false;
        await h.Workflow.PrintStoredAsync(id);
        Assert.Equal(correction.Pdf.ToArray(), Assert.Single(h.Printer.Printed));
    }

    /// <summary>修订保存失败不改变旧档案；两个审核预览竞争提交时，后提交者必须重新审核。</summary>
    [Fact]
    public async Task Revision_failure_and_stale_preview_do_not_replace_current_version()
    {
        var h = new Harness();
        var id = await h.Workflow.IssueAsync(await h.Workflow.PrepareAsync(h.Workspace, h.Run, true));
        var originalBytes = h.Store.Documents[id].Content.ToArray();
        var first = await h.Workflow.PrepareRevisionAsync(h.Workspace, h.Run, id);
        var stale = await h.Workflow.PrepareRevisionAsync(h.Workspace, h.Run, id);
        h.Store.FailSave = true;
        await Assert.ThrowsAsync<IOException>(() => h.Workflow.IssueAsync(first).AsTask());
        Assert.Equal(originalBytes, h.Store.Documents[id].Content.ToArray());
        h.Store.FailSave = false;
        await h.Workflow.IssueAsync(first);
        await Assert.ThrowsAsync<InvalidDataException>(() => h.Workflow.IssueAsync(stale).AsTask());
        Assert.Equal(first.Draft.Report.Metadata.VersionId, (await h.Workflow.ReadStoredAsync(id)).Report.Metadata.VersionId);
    }

    /// <summary>同名量表的新一次评估不能更正旧评估报告，候选查询也不会混入它。</summary>
    [Fact]
    public async Task Revision_requires_the_same_assessment_instance()
    {
        var h = new Harness();
        var id = await h.Workflow.IssueAsync(await h.Workflow.PrepareAsync(h.Workspace, h.Run, true));
        var service = new NutritionAssessmentApplicationService([new MustInstrument()]);
        Assert.Single((await h.Workflow.ListRevisableAsync(h.Workspace, h.Run)).Candidates);
        h.Workspace.NutritionAssessments.Remove(h.Run);
        var another = service.StartRun(h.Workspace, service.Definitions.Single());
        foreach (var item in h.Run.Definition.Sections.SelectMany(section => section.Items))
            another.SetAnswer(item.Code, h.Run.GetAnswer(item.Code)!);
        Assert.Empty((await h.Workflow.ListRevisableAsync(h.Workspace, another)).Candidates);
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Workflow.PrepareRevisionAsync(h.Workspace, another, id).AsTask());
    }

    /// <summary>导入完整修订包保留所有历史原件；历史 PDF 损坏也必须拒绝整个包。</summary>
    [Fact]
    public async Task Revision_package_round_trip_checks_every_original()
    {
        var h = new Harness();
        var id = await h.Workflow.IssueAsync(await h.Workflow.PrepareAsync(h.Workspace, h.Run, true));
        var initial = h.Store.Documents[id].Content.ToArray();
        var target = new Harness();
        await target.Workflow.ImportAsync(new ExternalArchiveDocument { Content = initial });
        await h.Workflow.IssueAsync(await h.Workflow.PrepareRevisionAsync(h.Workspace, h.Run, id));
        await h.Workflow.IssueAsync(await h.Workflow.PrepareRevisionAsync(h.Workspace, h.Run, id));
        await target.Workflow.ImportAsync(new ExternalArchiveDocument { Content = h.Store.Documents[id].Content });
        Assert.Equal(3, (await target.Workflow.ReadStoredAsync(id)).Versions.Count);
        await Assert.ThrowsAsync<InvalidDataException>(() => target.Workflow.ImportAsync(
            new ExternalArchiveDocument { Content = initial }).AsTask());
        using var corrupt = new MemoryStream();
        corrupt.Write(h.Store.Documents[id].Content.Span);
        using (var zip = new ZipArchive(corrupt, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = zip.Entries.First(item => item.FullName.StartsWith("history/", StringComparison.Ordinal));
            using var body = entry.Open();
            body.SetLength(0);
            body.Write("%PDF-1.7\nchanged"u8);
        }
        await Assert.ThrowsAsync<InvalidDataException>(() => target.Workflow.ImportAsync(
            new ExternalArchiveDocument { Content = corrupt.ToArray() }).AsTask());
    }

    /// <summary>版本标识和旧 PDF 都相同也不足以覆盖本机历史，导入必须保留旧资源的全部事实。</summary>
    [Fact]
    public async Task Import_rejects_modified_historical_facts_even_with_unchanged_pdf()
    {
        var source = new Harness();
        var id = await source.Workflow.IssueAsync(await source.Workflow.PrepareAsync(source.Workspace, source.Run, true));
        var target = new Harness();
        await target.Workflow.ImportAsync(new ExternalArchiveDocument { Content = source.Store.Documents[id].Content });
        await source.Workflow.IssueAsync(await source.Workflow.PrepareRevisionAsync(source.Workspace, source.Run, id));
        var revised = await source.Workflow.ReadStoredAsync(id);
        var changed = revised with
        {
            Document = revised.Document with
            {
                Bundle = revised.Document.Bundle with
                {
                    Entries = revised.Document.Bundle.Entries.Select(resource =>
                        resource is NutritionReportResource report && report.Metadata.RevisionNumber.Value == 1
                            ? report with { Title = "被改写的旧版标题" } : resource).ToArray()
                }
            }
        };
        var bytes = await source.Package.WriteAsync(changed);
        await Assert.ThrowsAsync<InvalidDataException>(() => target.Workflow.ImportAsync(
            new ExternalArchiveDocument { Content = bytes }).AsTask());
        Assert.Single((await target.Workflow.ReadStoredAsync(id)).Versions);
    }

    /// <summary>允许报告引用多个患者版本作为输入，调阅时使用咨询的明确对象快照。</summary>
    [Fact]
    public async Task Report_review_handles_multiple_patient_input_versions_without_choosing_one_arbitrarily()
    {
        var source = new Harness();
        var id = await source.Workflow.IssueAsync(await source.Workflow.PrepareAsync(source.Workspace, source.Run, true));
        ((ClientInfo)source.Workspace.Client).Name = "更正后患者";
        await source.Workflow.IssueAsync(await source.Workflow.PrepareRevisionAsync(source.Workspace, source.Run, id));
        var revised = await source.Workflow.ReadStoredAsync(id);
        var oldPatient = revised.Document.Bundle.Entries.OfType<PatientResource>().First();
        var current = revised.Report;
        var changed = revised with
        {
            Document = revised.Document with
            {
                Bundle = revised.Document.Bundle with
                {
                    Entries = revised.Document.Bundle.Entries.Select(resource => resource.Metadata.VersionId == current.Metadata.VersionId
                        ? current with { InputResourceReferences = current.InputResourceReferences.Append(
                            new VersionedResourceReference(oldPatient.Metadata.ResourceId, oldPatient.Metadata.VersionId, oldPatient.ResourceType)).ToArray() }
                        : resource).ToArray()
                }
            }
        };
        var target = new Harness();
        await target.Workflow.ImportAsync(new ExternalArchiveDocument { Content = await source.Package.WriteAsync(changed) });
        var opened = await target.Archives.OpenStoredAsync(id);
        Assert.True(opened.Operation.IsSuccess);
        Assert.NotNull(opened.Review);
        Assert.Equal("更正后患者", opened.Review.SubjectDisplay);
    }

    /// <summary>版本 2 读取器保留对已发出的版本 1 单报告容器的兼容。</summary>
    [Fact]
    public async Task Version_one_single_report_packages_remain_readable()
    {
        var h = new Harness();
        var id = await h.Workflow.IssueAsync(await h.Workflow.PrepareAsync(h.Workspace, h.Run, true));
        using var legacy = new MemoryStream();
        legacy.Write(h.Store.Documents[id].Content.Span);
        using (var zip = new ZipArchive(legacy, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = zip.GetEntry("manifest.json")!;
            JsonNode manifest;
            using (var body = entry.Open()) manifest = JsonNode.Parse(body)!;
            manifest["version"] = 1;
            using var output = entry.Open();
            output.SetLength(0);
            output.Write(Encoding.UTF8.GetBytes(manifest.ToJsonString()));
        }
        var target = new Harness();
        Assert.Equal(id, await target.Workflow.ImportAsync(new ExternalArchiveDocument { Content = legacy.ToArray() }));
        Assert.Equal("1", target.Store.Documents[id].Info.FormatVersion);
        Assert.Single((await target.Workflow.ReadStoredAsync(id)).Versions);
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
        public ReportPackage Package { get; }

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
            Package = new(codecs, validator);
            Workflow = new(new(assembler), Renderer, Access, validator, Package, Store, Printer, new(assembler), Renderer);
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

    private sealed class Renderer : IAssessmentReportRenderer, IDietaryReportRenderer
    {
        public DietaryReportDraft? DietaryDraft { get; private set; }
        public ValueTask<byte[]> RenderAsync(DietaryReportDraft draft, CancellationToken cancellationToken = default)
        {
            Calls++;
            DietaryDraft = draft;
            return ValueTask.FromResult(Encoding.UTF8.GetBytes($"%PDF-1.7\ndietary revision {draft.Report.Metadata.RevisionNumber.Value}"));
        }
        public int Calls { get; private set; }
        public NutritionAssessmentSnapshot? Evaluation { get; private set; }
        public Action? AfterEvaluation { get; set; }
        public ValueTask<byte[]> RenderEvaluationAsync(NutritionAssessmentSnapshot snapshot, DateTimeOffset generatedAt,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            Evaluation = snapshot;
            AfterEvaluation?.Invoke();
            return ValueTask.FromResult(Encoding.UTF8.GetBytes("%PDF-1.7\nevaluation"));
        }
        public CanonicalReference Template { get; } = new(new Uri("urn:test:template"), "1");
        public ValueTask<byte[]> RenderAsync(AssessmentReportDraft draft, CancellationToken cancellationToken = default)
        {
            Calls++;
            return ValueTask.FromResult(Encoding.UTF8.GetBytes($"%PDF-1.7\nfixture {draft.Assessment.TotalScore} revision {draft.Report.Metadata.RevisionNumber.Value}"));
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
        public Func<Guid, Exception?>? ReadFailure { get; set; }
        public Exception? ListFailure { get; set; }
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
            ListFailure is { } exception ? throw exception
                : ValueTask.FromResult<IReadOnlyList<StoredArchiveDocumentInfo>>(Documents.Values.Select(doc => doc.Info).ToArray());
        public ValueTask<StoredArchiveDocument?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            ReadFailure?.Invoke(id) is { } exception ? throw exception
                : ValueTask.FromResult(Documents.GetValueOrDefault(id));
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
