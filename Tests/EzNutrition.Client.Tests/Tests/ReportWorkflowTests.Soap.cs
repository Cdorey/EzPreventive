using EzNutrition.Application.Reports;
using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Domain.Consultations;
using EzNutrition.Presentation.Reports;
using Microsoft.AspNetCore.Components;

namespace EzNutrition.Client.Tests.Tests;

public sealed partial class ReportWorkflowTests
{
    private static Harness SoapHarness()
    {
        var h = new Harness();
        h.Workspace.SubjectiveObjectiveAssessmentPlanInformation = new()
        {
            Subjective = "合成主观资料\n第二行记录", Objective = "合成客观资料",
            Assessment = "合成问题评估", Plan = "合成处理计划"
        };
        return h;
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Soap_permissions_are_independent(bool issue, bool print)
    {
        var h = SoapHarness();
        h.Access.Issue = issue;
        h.Access.Print = print;
        if (issue) await h.Workflow.IssueAsync(await h.Workflow.PrepareSoapAsync(h.Workspace, true));
        else await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Workflow.PrepareSoapAsync(h.Workspace, true).AsTask());
        if (print) await h.Workflow.PrintEvaluationAsync(await h.Workflow.PrepareSoapAsync(h.Workspace, false));
        else await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Workflow.PrepareSoapAsync(h.Workspace, false).AsTask());
        Assert.Equal(issue ? 1 : 0, h.Store.Saves);
        Assert.Equal(print ? 1 : 0, h.Printer.Printed.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Soap_missing_or_whitespace_note_is_rejected(bool whitespace)
    {
        var h = new Harness();
        if (whitespace) h.Workspace.SubjectiveObjectiveAssessmentPlanInformation = new() { Subjective = " \n\t " };
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Workflow.PrepareSoapAsync(h.Workspace, false).AsTask());
        Assert.Equal(0, h.Renderer.Calls);
    }

    [Theory]
    [InlineData("subjective")]
    [InlineData("objective")]
    [InlineData("assessment")]
    [InlineData("plan")]
    [InlineData("replacement")]
    [InlineData("patient")]
    public async Task Soap_changes_during_review_block_issuance(string changed)
    {
        var h = SoapHarness();
        var preview = await h.Workflow.PrepareSoapAsync(h.Workspace, true);
        var note = h.Workspace.SubjectiveObjectiveAssessmentPlanInformation!;
        switch (changed)
        {
            case "subjective": note.Subjective += "修改"; break;
            case "objective": note.Objective += "修改"; break;
            case "assessment": note.Assessment += "修改"; break;
            case "plan": note.Plan += "修改"; break;
            case "replacement": h.Workspace.SubjectiveObjectiveAssessmentPlanInformation = new(); break;
            case "patient": h.Workspace.Client.Weight = 80; break;
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Workflow.IssueAsync(preview).AsTask());
        Assert.Empty(h.Store.Documents);
    }

    [Fact]
    public async Task Soap_empty_sections_keep_titles_and_blank_body_and_plain_text_is_preserved()
    {
        var h = SoapHarness();
        h.Workspace.SubjectiveObjectiveAssessmentPlanInformation = new() { Plan = "第一行\n<script>原文</script>\n- 第二项" };
        var preview = await h.Workflow.PrepareSoapAsync(h.Workspace, true);
        var model = SoapReportPdfModel.From(Assert.IsType<SoapReportDraft>(preview.Draft));
        Assert.Equal(4, model.Sections.Count);
        Assert.All(model.Sections.Take(3), section => { Assert.NotEmpty(section.Title); Assert.Equal("", section.Text); });
        Assert.Equal(h.Workspace.SubjectiveObjectiveAssessmentPlanInformation.Plan, model.Sections[3].Text);
        await h.Workflow.IssueAsync(preview);
        Assert.Single(h.Store.Documents);
    }

    [Fact]
    public async Task Soap_snapshots_and_originals_survive_correction_import_and_workspace_changes()
    {
        var h = SoapHarness();
        var preview = await h.Workflow.PrepareSoapAsync(h.Workspace, true);
        var note = Assert.Single(preview.Draft.Document.Bundle.Entries.OfType<SoapNoteResource>());
        Assert.Equal(4, preview.Draft.Document.Bundle.Entries.Count);
        Assert.DoesNotContain(preview.Draft.Document.Bundle.Entries, resource => resource is NutritionScaleAssessmentResource or EnergyAssessmentResource or DietaryRecallResource);
        var id = await h.Workflow.IssueAsync(preview);
        Assert.Equal(id, await h.Workflow.IssueAsync(preview));
        var scale = await h.Workflow.IssueAsync(await h.Workflow.PrepareAsync(h.Workspace, h.Run, true));
        Assert.Equal(id, Assert.Single((await h.Workflow.ListSoapRevisableAsync(h.Workspace)).Candidates).DocumentId);
        Assert.Equal(scale, Assert.Single((await h.Workflow.ListRevisableAsync(h.Workspace, h.Run)).Candidates).DocumentId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Workflow.PrepareSoapAsync(h.Workspace, true, scale).AsTask());
        h.Workspace.SubjectiveObjectiveAssessmentPlanInformation!.Plan = "修改后的合成计划";
        var revision = await h.Workflow.PrepareSoapAsync(h.Workspace, true, id);
        await h.Workflow.IssueAsync(revision);
        Assert.Equal("合成处理计划", note.Plan);
        var stored = await h.Workflow.ReadStoredAsync(id);
        Assert.Equal(preview.Pdf.ToArray(), stored.PreviousPdfs[preview.Draft.Report.Metadata.VersionId.Value].ToArray());
        await h.Workflow.PrintStoredAsync(id);
        Assert.Equal(revision.Pdf.ToArray(), Assert.Single(h.Printer.Printed));
        Assert.True((await h.Archives.OpenStoredAsync(id)).Operation.IsSuccess);
        var target = new Harness();
        await target.Workflow.ImportAsync(new() { Content = h.Store.Documents[id].Content });
        await target.Workflow.PrintStoredAsync(id);
        Assert.Equal(revision.Pdf.ToArray(), Assert.Single(target.Printer.Printed));
    }

    [Fact]
    public async Task Soap_panel_previews_directly_and_preview_failure_cannot_be_issued()
    {
        var h = SoapHarness();
        var js = new PreviewRuntime { Failure = "createPreview" };
        using var panel = CreatePanel(h, js);
        ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(ReportPanel.Kind)] = ReportKind.Soap }).SetParameterProperties(panel);
        await InvokePanelAsync(panel, "BeginPrepareAsync", true, null);
        await InvokePanelAsync(panel, "IssueAsync");
        Assert.Empty(h.Store.Documents);
        js.Failure = null;
        await InvokePanelAsync(panel, "BeginPrepareAsync", true, null);
        await InvokePanelAsync(panel, "IssueAsync");
        Assert.Single(h.Store.Documents);
    }

    [Fact]
    public async Task Soap_renderer_uses_frozen_plain_text_and_can_export_synthetic_fixture()
    {
        var h = SoapHarness();
        var js = new DietaryPdfRuntime();
        var renderer = new PdfMakeSoapReportRenderer(js);
        var factory = new SoapReportFactory(new(new(new Uri("urn:test:soap"), "SOAP 报告测试", "1")));
        var draft = factory.Create(h.Workspace, renderer.Template, h.Access.Actor, DateTimeOffset.UtcNow);
        h.Workspace.SubjectiveObjectiveAssessmentPlanInformation!.Subjective = "后续修改";
        await renderer.RenderAsync(draft);
        var json = js.Model!.Value;
        Assert.Equal("合成主观资料\n第二行记录", json.GetProperty("sections")[0].GetProperty("text").GetString());
        Assert.False(json.GetProperty("isEvaluation").GetBoolean());
        Assert.True(js.Disposed);
        if (Environment.GetEnvironmentVariable("EZNUTRITION_REPORT_TEST_OUTPUT") is { Length: > 0 } output)
        {
            Directory.CreateDirectory(output);
            await File.WriteAllTextAsync(Path.Combine(output, "soap-model.json"), json.GetRawText());
        }
    }
}
