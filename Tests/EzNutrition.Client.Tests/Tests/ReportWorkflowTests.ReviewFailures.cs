using System.Reflection;
using EzNutrition.Application.Archives;
using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Archives.Contracts.Serialization;
using EzNutrition.Archives.Contracts.Validation;
using EzNutrition.Archives.Xml;
using EzNutrition.Presentation.Reports;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace EzNutrition.Client.Tests.Tests;

public sealed partial class ReportWorkflowTests
{
    /// <summary>同一份 XML 包含报告和额外 SOAP 时，只有报告包调阅限制在报告输入内。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Review_scopes_inputs_only_for_report_packages(bool reportPackage)
    {
        var h = new Harness();
        var id = await h.Workflow.IssueAsync(await h.Workflow.PrepareAsync(h.Workspace, h.Run, true));
        var signed = await h.Workflow.ReadStoredAsync(id);
        var soap = new SoapNoteResource
        {
            Metadata = signed.Document.Bundle.Entries.OfType<NutritionScaleAssessmentResource>().Single().Metadata with
            {
                ResourceId = new ResourceId(Guid.NewGuid()),
                VersionId = new ResourceVersionId(Guid.NewGuid())
            },
            SubjectReference = signed.Report.SubjectReference,
            ConsultationReference = signed.Report.ConsultationReference,
            EffectiveAt = signed.Consultation.Period.Start,
            Subjective = "未纳入量表报告的咨询资料"
        };
        var document = signed.Document with
        {
            Bundle = signed.Document.Bundle with
            {
                Entries = signed.Document.Bundle.Entries.Select(resource => resource is ConsultationResource consultation
                    ? consultation with
                    {
                        ClinicalResourceReferences = consultation.ClinicalResourceReferences.Append(
                            new VersionedResourceReference(soap.Metadata.ResourceId, soap.Metadata.VersionId, soap.ResourceType)).ToArray()
                    } : resource).Append(soap).ToArray()
            }
        };
        var stored = h.Store.Documents[id];
        if (reportPackage)
            h.Store.Documents[id] = stored with { Content = await h.Package.WriteAsync(signed with { Document = document }) };
        else
        {
            using var stream = new MemoryStream();
            var write = await new XmlArchiveCodec(new ArchiveContractValidator()).WriteAsync(
                new ArchiveWriteRequest { Document = document, TargetFormat = XmlArchiveFormat.Current }, stream);
            Assert.True(write.IsSuccess, string.Join(", ", write.Validation.Issues.Select(issue => issue.Code)));
            h.Store.Documents[id] = stored with
            {
                Content = stream.ToArray(),
                Info = stored.Info with
                {
                    FormatIdentifier = XmlArchiveFormat.Current.Identifier.AbsoluteUri,
                    FormatVersion = XmlArchiveFormat.Current.Version,
                    MediaType = XmlArchiveFormat.Current.MediaType!
                }
            };
        }

        var opened = await h.Archives.OpenStoredAsync(id);
        Assert.True(opened.Operation.IsSuccess, opened.Operation.Message);
        var review = Assert.IsType<ArchiveReview>(opened.Review);
        Assert.Equal(!reportPackage, review.Sections.Any(section => section.Title == "SOAP 病史"));
        Assert.Equal(reportPackage, review.FormatDisplay == "报告档案");
        Assert.Equal(reportPackage ? signed.Report.Title : signed.Consultation.Title, review.Title);
    }

    /// <summary>损坏包不能遮蔽同一患者的正常更正对象，也不能被直接更正或打印。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Revision_list_keeps_healthy_reports_when_another_package_is_corrupt(bool corruptLast)
    {
        var h = new Harness();
        var firstId = await h.Workflow.IssueAsync(await h.Workflow.PrepareAsync(h.Workspace, h.Run, true));
        var secondId = await h.Workflow.IssueAsync(await h.Workflow.PrepareAsync(h.Workspace, h.Run, true));
        var badId = corruptLast ? secondId : firstId;
        var goodId = corruptLast ? firstId : secondId;
        h.Store.Documents[badId] = h.Store.Documents[badId] with { Content = "broken report"u8.ToArray() };

        var candidates = await h.Workflow.ListRevisableAsync(h.Workspace, h.Run);
        Assert.Equal(goodId, Assert.Single(candidates.Candidates).DocumentId);
        Assert.Equal(1, candidates.UnreadableCount);
        await h.Workflow.PrepareRevisionAsync(h.Workspace, h.Run, goodId);
        await Assert.ThrowsAsync<InvalidDataException>(() => h.Workflow.PrepareRevisionAsync(h.Workspace, h.Run, badId).AsTask());
        await Assert.ThrowsAsync<InvalidDataException>(() => h.Workflow.PrintStoredAsync(badId).AsTask());
    }

    /// <summary>单份文件读取异常可以跳过，但取消、权限或整个目录不可用不能伪装成空列表。</summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("io")]
    [InlineData("cancel")]
    [InlineData("denied")]
    [InlineData("directory")]
    public async Task Revision_list_distinguishes_document_failures_from_query_failures(string failure)
    {
        var h = new Harness();
        var id = await h.Workflow.IssueAsync(await h.Workflow.PrepareAsync(h.Workspace, h.Run, true));
        Exception error = failure switch
        {
            "missing" => new FileNotFoundException(),
            "cancel" => new OperationCanceledException(),
            "denied" => new UnauthorizedAccessException(),
            _ => new IOException()
        };
        if (failure == "directory") h.Store.ListFailure = error;
        else h.Store.ReadFailure = readId => readId == id ? error : null;

        if (failure is "missing" or "io")
        {
            var listed = await h.Workflow.ListRevisableAsync(h.Workspace, h.Run);
            Assert.Empty(listed.Candidates);
            Assert.Equal(1, listed.UnreadableCount);
        }
        else
            Assert.Same(error, await Record.ExceptionAsync(() => h.Workflow.ListRevisableAsync(h.Workspace, h.Run).AsTask()));
    }

    /// <summary>全部报告都损坏时明确显示跳过原因，不误报为从未签发。</summary>
    [Fact]
    public async Task Panel_explains_when_all_revision_candidates_are_unreadable()
    {
        var h = new Harness();
        var id = await h.Workflow.IssueAsync(await h.Workflow.PrepareAsync(h.Workspace, h.Run, true));
        h.Store.Documents[id] = h.Store.Documents[id] with { Content = "broken report"u8.ToArray() };
        using var panel = CreatePanel(h, new PreviewRuntime());

        await InvokePanelAsync(panel, "ChooseRevisionAsync");

        var feedback = (string)typeof(AssessmentReportPanel)
            .GetField("feedback", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(panel)!;
        Assert.Contains("已跳过 1 份", feedback);
        Assert.Contains("其余报告中没有", feedback);
    }

    /// <summary>实际组件事件中注入预览故障；失败后确认不得归档，重试成功后才可签发。</summary>
    [Theory]
    [InlineData("import")]
    [InlineData("createPreview")]
    [InlineData("emptyPreview")]
    [InlineData("releasePreview")]
    [InlineData("dispose")]
    public async Task Panel_does_not_issue_after_preview_failure_and_can_retry(string failure)
    {
        var h = new Harness();
        var js = new PreviewRuntime();
        using var panel = CreatePanel(h, js);
        if (failure == "releasePreview") await InvokePanelAsync(panel, "PrepareAsync", true, null);
        js.Failure = failure;

        await InvokePanelAsync(panel, "PrepareAsync", true, null);
        await InvokePanelAsync(panel, "IssueAsync");
        Assert.Empty(h.Store.Documents);

        js.Failure = null;
        await InvokePanelAsync(panel, "PrepareAsync", true, null);
        await InvokePanelAsync(panel, "IssueAsync");
        Assert.Single(h.Store.Documents);
    }

    private static AssessmentReportPanel CreatePanel(Harness h, PreviewRuntime js)
    {
        var panel = new AssessmentReportPanel();
        ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(AssessmentReportPanel.Workspace)] = h.Workspace,
            [nameof(AssessmentReportPanel.Assessment)] = h.Run
        }).SetParameterProperties(panel);
        SetPanelProperty(panel, "Workflow", h.Workflow);
        SetPanelProperty(panel, "JS", js);
        return panel;
    }

    private static void SetPanelProperty(AssessmentReportPanel panel, string name, object value) =>
        typeof(AssessmentReportPanel).GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(panel, value);

    private static Task InvokePanelAsync(AssessmentReportPanel panel, string name, params object?[] arguments) =>
        (Task)typeof(AssessmentReportPanel).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(panel, arguments)!;

    private sealed class PreviewRuntime : IJSRuntime, IJSObjectReference
    {
        public string? Failure { get; set; }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, default, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (identifier == Failure) throw new JSException("模拟预览失败");
            object? result = identifier switch
            {
                "import" => this,
                "createPreview" => Failure == "emptyPreview" ? "" : "blob:report-preview",
                _ => default(TValue)
            };
            return ValueTask.FromResult((TValue)result!);
        }

        public ValueTask DisposeAsync() => Failure == "dispose"
            ? throw new JSException("模拟 JS 模块释放失败") : ValueTask.CompletedTask;
    }
}
