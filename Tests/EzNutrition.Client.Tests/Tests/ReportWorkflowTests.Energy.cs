using EzNutrition.Application.Reports;
using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Domain.Assessments;
using EzNutrition.Presentation.Reports;
using EzNutrition.Shared.Data.Entities;
using Microsoft.AspNetCore.Components;

namespace EzNutrition.Client.Tests.Tests;

public sealed partial class ReportWorkflowTests
{
    private static Harness EnergyHarness()
    {
        var h = new Harness();
        h.Workspace.CurrentEnergyCalculator = new EnergyCalculator(h.Workspace.Client);
        var energy = h.Workspace.CurrentEnergyCalculator!;
        energy.PAL = 1.5m;
        energy.AvailableEERs = [new EER { BEE = 25, PAL = 1.5m, AvgBwEER = 2000 }];
        Assert.True(energy.Calculate());
        Assert.True(energy.CorrectEnergy(2000));
        return h;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Energy_configuration_waits_for_confirmation_and_cancel_creates_no_report(bool forIssuance)
    {
        var h = EnergyHarness();
        using var panel = CreatePanel(h, new PreviewRuntime());
        ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(ReportPanel.Kind)] = ReportKind.Energy }).SetParameterProperties(panel);
        await InvokePanelAsync(panel, "BeginPrepareAsync", forIssuance, null);
        Assert.Equal(0, h.Renderer.Calls);
        await InvokePanelAsync(panel, "IssueAsync");
        Assert.Empty(h.Store.Documents);
        await InvokePanelAsync(panel, "CloseAsync");
        var options = new EnergyReportOptions(false, true, false);
        await InvokePanelAsync(panel, "ConfirmEnergyConfigurationAsync", options);
        Assert.Equal(0, h.Renderer.Calls);
        await InvokePanelAsync(panel, "BeginPrepareAsync", forIssuance, null);
        await InvokePanelAsync(panel, "ConfirmEnergyConfigurationAsync", options);
        Assert.Equal(options, h.Renderer.EnergyDraft!.Options);
        Assert.Equal(forIssuance, h.Renderer.EnergyDraft.Signer is not null);
    }

    [Fact]
    public async Task Energy_empty_sections_are_rejected_before_rendering()
    {
        var h = EnergyHarness();
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Workflow.PrepareEnergyAsync(h.Workspace, true,
            options: new(false, false, false)).AsTask());
        Assert.Equal(0, h.Renderer.Calls);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Energy_permissions_remain_independent(bool issue, bool print)
    {
        var h = EnergyHarness();
        h.Access.Issue = issue;
        h.Access.Print = print;
        if (issue) await h.Workflow.IssueAsync(await h.Workflow.PrepareEnergyAsync(h.Workspace, true));
        else await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Workflow.PrepareEnergyAsync(h.Workspace, true).AsTask());
        if (print) await h.Workflow.PrintEvaluationAsync(await h.Workflow.PrepareEnergyAsync(h.Workspace, false));
        else await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Workflow.PrepareEnergyAsync(h.Workspace, false).AsTask());
        Assert.Equal(issue ? 1 : 0, h.Store.Saves);
        Assert.Equal(print ? 1 : 0, h.Printer.Printed.Count);
    }

    [Theory]
    [InlineData("energy")]
    [InlineData("allocation")]
    [InlineData("exchanges")]
    [InlineData("patient")]
    [InlineData("pal")]
    public async Task Energy_changed_during_review_requires_new_preview(string changed)
    {
        var h = EnergyHarness();
        var preview = await h.Workflow.PrepareEnergyAsync(h.Workspace, true);
        var calculator = h.Workspace.CurrentEnergyCalculator!;
        switch (changed)
        {
            case "energy": calculator.CorrectEnergy(2200); break;
            case "allocation": calculator.Allocation!.ProteinPercentage = .2; break;
            case "exchanges": calculator.FoodExchangeAllocation!.Fruits = 2; break;
            case "patient": h.Workspace.Client.Weight = 80; break;
            case "pal": calculator.PAL = 1.8m; break;
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Workflow.IssueAsync(preview).AsTask());
        Assert.Empty(h.Store.Documents);
    }

    [Theory]
    [InlineData("not-calculated")]
    [InlineData("stale-input")]
    [InlineData("negative-exchanges")]
    [InlineData("invalid-fraction")]
    [InlineData("changed-reference")]
    public async Task Energy_invalid_results_cannot_enter_preview(string state)
    {
        var h = state == "not-calculated" ? new Harness() : EnergyHarness();
        var calculator = h.Workspace.CurrentEnergyCalculator!;
        if (state == "stale-input") h.Workspace.Client.Height = 190;
        if (state == "negative-exchanges") calculator.FoodExchangeAllocation!.Fruits = 100;
        if (state == "invalid-fraction") calculator.Allocation!.ProteinPercentage = double.NaN;
        if (state == "changed-reference") calculator.SelectedEer!.BEE = 30;
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Workflow.PrepareEnergyAsync(h.Workspace, true).AsTask());
        Assert.Equal(0, h.Renderer.Calls);
    }

    [Fact]
    public async Task Energy_snapshot_retains_results_and_originals_across_correction_and_import()
    {
        var h = EnergyHarness();
        var preview = await h.Workflow.PrepareEnergyAsync(h.Workspace, true, options: new(false, false, true));
        var draft = Assert.IsType<EnergyReportDraft>(preview.Draft);
        var energy = Assert.Single(draft.Document.Bundle.Entries.OfType<EnergyAssessmentResource>());
        Assert.DoesNotContain(draft.Document.Bundle.Entries, resource => resource is DietaryRecallResource or NutritionScaleAssessmentResource or SoapNoteResource);
        Assert.Equal(2000, energy.ProfessionalDecision!.AdoptedEnergyTarget.Value);
        Assert.Equal(3, energy.AllocationPlan!.MacronutrientTargets.Count);
        Assert.Equal(6, energy.AllocationPlan.FoodExchangeTargets.Count);
        var model = EnergyReportPdfModel.From(draft);
        Assert.Equal("2000 kcal/日", model.EnergyTarget);
        Assert.Equal("1", model.MealExchanges[0][1]);
        Assert.False(model.Options.IncludeTotalEnergy);
        Assert.Equal("22 g", model.Meals[0][1]);
        Assert.Contains(model.TotalEnergy, row => row[0] == "采用依据" && row[1] == "专业人员手工核定");
        var id = await h.Workflow.IssueAsync(preview);
        Assert.Equal(id, await h.Workflow.IssueAsync(preview));
        var scale = await h.Workflow.IssueAsync(await h.Workflow.PrepareAsync(h.Workspace, h.Run, true));
        Assert.Equal(id, Assert.Single((await h.Workflow.ListEnergyRevisableAsync(h.Workspace)).Candidates).DocumentId);
        Assert.Equal(scale, Assert.Single((await h.Workflow.ListRevisableAsync(h.Workspace, h.Run)).Candidates).DocumentId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Workflow.PrepareEnergyAsync(h.Workspace, true, scale).AsTask());
        h.Workspace.CurrentEnergyCalculator!.CorrectEnergy(2200);
        var revision = await h.Workflow.PrepareEnergyAsync(h.Workspace, true, id);
        await h.Workflow.IssueAsync(revision);
        var stored = await h.Workflow.ReadStoredAsync(id);
        Assert.Equal(preview.Pdf.ToArray(), stored.PreviousPdfs[draft.Report.Metadata.VersionId.Value].ToArray());
        Assert.Equal(2000, energy.ProfessionalDecision.AdoptedEnergyTarget.Value);
        await h.Workflow.PrintStoredAsync(id);
        Assert.Equal(revision.Pdf.ToArray(), Assert.Single(h.Printer.Printed));
        var target = new Harness();
        await target.Workflow.ImportAsync(new() { Content = h.Store.Documents[id].Content });
        await target.Workflow.PrintStoredAsync(id);
        Assert.Equal(revision.Pdf.ToArray(), Assert.Single(target.Printer.Printed));
    }

    [Fact]
    public async Task Energy_renderer_passes_frozen_model_and_can_export_synthetic_fixture()
    {
        var h = EnergyHarness();
        var js = new DietaryPdfRuntime();
        var renderer = new PdfMakeEnergyReportRenderer(js);
        var factory = new EnergyReportFactory(new(new(new Uri("urn:test:energy"), "能量报告测试", "1")));
        var draft = factory.Create(h.Workspace, renderer.Template, h.Access.Actor, DateTimeOffset.UtcNow);
        await renderer.RenderAsync(draft);
        var json = js.Model!.Value;
        Assert.Equal("2000 kcal/日", json.GetProperty("energyTarget").GetString());
        Assert.Equal(3, json.GetProperty("mealExchanges").GetArrayLength());
        Assert.True(json.GetProperty("options").GetProperty("includeAllocation").GetBoolean());
        Assert.True(js.Disposed);
        if (Environment.GetEnvironmentVariable("EZNUTRITION_REPORT_TEST_OUTPUT") is { Length: > 0 } output)
        {
            Directory.CreateDirectory(output);
            await File.WriteAllTextAsync(Path.Combine(output, "energy-model.json"), json.GetRawText());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Energy_manual_only_and_population_estimates_can_be_signed(bool automatic)
    {
        var h = new Harness();
        h.Workspace.CurrentEnergyCalculator = new EnergyCalculator(h.Workspace.Client);
        var calculator = h.Workspace.CurrentEnergyCalculator;
        if (automatic)
        {
            calculator.PAL = 1.5m;
            calculator.AvailableEERs = [new EER { PAL = 1.5m, AvgBwEER = 2000 }];
            calculator.Calculate();
        }
        else calculator.CorrectEnergy(2000);
        var preview = await h.Workflow.PrepareEnergyAsync(h.Workspace, true);
        var energy = preview.Draft.Document.Bundle.Entries.OfType<EnergyAssessmentResource>().Single();
        Assert.Equal(automatic ? 1 : 0, energy.CandidateCalculations.Count);
        var model = EnergyReportPdfModel.From(Assert.IsType<EnergyReportDraft>(preview.Draft));
        Assert.Contains(model.TotalEnergy, row => row[0] == "采用依据"
            && row[1] == (automatic ? "采用自动计算结果" : "专业人员手工核定"));
        await h.Workflow.IssueAsync(preview);
        Assert.Single(h.Store.Documents);
    }
}
