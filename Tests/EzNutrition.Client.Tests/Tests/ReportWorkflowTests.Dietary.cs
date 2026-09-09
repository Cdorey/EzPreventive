using System.Text.Json;
using EzNutrition.Application.Reports;
using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Client.Tests.Fixtures;
using EzNutrition.Domain.Dietary;
using EzNutrition.Presentation.Reports;
using EzNutrition.Shared.Data.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace EzNutrition.Client.Tests.Tests;

public sealed partial class ReportWorkflowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dietary_configuration_waits_for_confirmation_and_cancellation_discards_intent(bool forIssuance)
    {
        var h = DietaryHarness();
        using var panel = CreatePanel(h, new PreviewRuntime());
        ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(ReportPanel.IsDietary)] = true }).SetParameterProperties(panel);
        await InvokePanelAsync(panel, "BeginPrepareAsync", forIssuance, null);
        Assert.Equal(0, h.Renderer.Calls);
        await InvokePanelAsync(panel, "IssueAsync");
        Assert.Empty(h.Store.Documents);
        await InvokePanelAsync(panel, "CloseAsync");
        await InvokePanelAsync(panel, "ConfirmConfigurationAsync", new DietaryReportOptions(1, false));
        Assert.Equal(0, h.Renderer.Calls);

        await InvokePanelAsync(panel, "BeginPrepareAsync", forIssuance, null);
        await InvokePanelAsync(panel, "ConfirmConfigurationAsync", new DietaryReportOptions(1, false));
        Assert.Equal(1, h.Renderer.Calls);
        Assert.Equal(new DietaryReportOptions(1, false), h.Renderer.DietaryDraft!.Options);
        Assert.Equal(forIssuance, h.Renderer.DietaryDraft.Signer is not null);
    }

    [Fact]
    public async Task Dietary_options_limit_each_table_and_preserve_reference_inputs_and_old_pdf()
    {
        var h = DietaryHarness();
        var preview = await h.Workflow.PrepareDietaryAsync(h.Workspace, true, options: new(1, false));
        var draft = Assert.IsType<DietaryReportDraft>(preview.Draft);
        var model = DietaryReportPdfModel.From(draft);
        Assert.All(model.Contributions, table => Assert.Single(table.Rows));
        Assert.False(model.IncludeDriReferences);
        Assert.NotEmpty(model.References);
        Assert.NotEmpty(model.NutrientGroups);
        Assert.Single(draft.Document.Bundle.Entries.OfType<DriAssessmentResource>());
        var id = await h.Workflow.IssueAsync(preview);
        var revised = await h.Workflow.PrepareDietaryAsync(h.Workspace, true, id, new(null, true));
        var revisedModel = DietaryReportPdfModel.From(Assert.IsType<DietaryReportDraft>(revised.Draft));
        Assert.All(revisedModel.Contributions, table => Assert.Equal(2, table.Rows.Length));
        Assert.True(revisedModel.IncludeDriReferences);
        await h.Workflow.IssueAsync(revised);
        var stored = await h.Workflow.ReadStoredAsync(id);
        Assert.Equal(preview.Pdf.ToArray(), stored.PreviousPdfs[draft.Report.Metadata.VersionId.Value].ToArray());
        Assert.Equal(new DietaryReportOptions(1, false), draft.Options);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Dietary_permissions_are_independent(bool issue, bool print)
    {
        var h = DietaryHarness();
        h.Access.Issue = issue;
        h.Access.Print = print;
        if (issue) await h.Workflow.IssueAsync(await h.Workflow.PrepareDietaryAsync(h.Workspace, true));
        else await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Workflow.PrepareDietaryAsync(h.Workspace, true).AsTask());
        if (print) await h.Workflow.PrintEvaluationAsync(await h.Workflow.PrepareDietaryAsync(h.Workspace, false));
        else await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Workflow.PrepareDietaryAsync(h.Workspace, false).AsTask());
        Assert.Equal(issue ? 1 : 0, h.Store.Saves);
        Assert.Equal(print ? 1 : 0, h.Printer.Printed.Count);
    }

    [Fact]
    public async Task Dietary_report_freezes_only_its_inputs_and_reprints_original()
    {
        var h = DietaryHarness();
        var preview = await h.Workflow.PrepareDietaryAsync(h.Workspace, true);
        var draft = Assert.IsType<DietaryReportDraft>(preview.Draft);
        var recall = draft.Document.Bundle.Entries.OfType<DietaryRecallResource>().Single();
        Assert.Single(draft.Document.Bundle.Entries.OfType<DriAssessmentResource>());
        Assert.DoesNotContain(draft.Document.Bundle.Entries, resource => resource is NutritionScaleAssessmentResource or SoapNoteResource);
        Assert.Equal(450m, recall.EnergyConsistency!.RecordedTotalEnergy.Value);
        var lunch = recall.Meals.Single(meal => meal.Sequence == 2).Entries.Single();
        Assert.Equal(200m, lunch.ReportedAmount.Value);
        Assert.Equal(150m, lunch.AdoptedConsumedAmount!.Value);
        Assert.Null(recall.RecallPeriod);
        Assert.NotNull(recall.RecallPeriodAbsentReason);

        var id = await h.Workflow.IssueAsync(preview);
        Assert.Equal(id, await h.Workflow.IssueAsync(preview));
        h.Workspace.DietaryRecallSurvey!.RecallEntries[0].Weight = 300;
        h.Workspace.DietaryRecallSurvey.Calculate();
        Assert.Equal(450m, recall.EnergyConsistency.RecordedTotalEnergy.Value);
        await h.Workflow.PrintStoredAsync(id);
        Assert.Equal(preview.Pdf.ToArray(), Assert.Single(h.Printer.Printed));
        Assert.True((await h.Archives.OpenStoredAsync(id)).Operation.IsSuccess);
        var target = new Harness();
        await target.Workflow.ImportAsync(new() { Content = h.Store.Documents[id].Content });
        await target.Workflow.PrintStoredAsync(id);
        Assert.Equal(preview.Pdf.ToArray(), Assert.Single(target.Printer.Printed));
    }

    [Theory]
    [InlineData("not-calculated")]
    [InlineData("edited")]
    [InlineData("empty")]
    public async Task Dietary_preparation_requires_current_calculation(string state)
    {
        var h = DietaryHarness();
        var survey = h.Workspace.DietaryRecallSurvey!;
        if (state == "not-calculated") survey.ResetCalculation();
        else if (state == "edited") survey.RecallEntries[0].Weight++;
        else { survey.RecallEntries.Clear(); survey.Calculate(); }
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Workflow.PrepareDietaryAsync(h.Workspace, true).AsTask());
        Assert.Empty(h.Store.Documents);
    }

    [Theory]
    [InlineData("edit")]
    [InlineData("recalculate")]
    [InlineData("patient")]
    [InlineData("reset")]
    public async Task Dietary_confirmation_rejects_changed_inputs(string change)
    {
        var h = DietaryHarness();
        var preview = await h.Workflow.PrepareDietaryAsync(h.Workspace, true);
        var survey = h.Workspace.DietaryRecallSurvey!;
        if (change == "edit") survey.RecallEntries[0].Weight++;
        else if (change == "recalculate") survey.Calculate();
        else if (change == "patient") ((EzNutrition.Domain.Consultations.ClientInfo)h.Workspace.Client).Height++;
        else survey.ResetCalculation();
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Workflow.IssueAsync(preview).AsTask());
        Assert.Empty(h.Store.Documents);
    }

    [Fact]
    public async Task Dietary_corrections_preserve_history_and_stay_separate_from_scale_reports()
    {
        var h = DietaryHarness();
        var scaleId = await h.Workflow.IssueAsync(await h.Workflow.PrepareAsync(h.Workspace, h.Run, true));
        var dietaryId = await h.Workflow.IssueAsync(await h.Workflow.PrepareDietaryAsync(h.Workspace, true));
        Assert.Equal(dietaryId, Assert.Single((await h.Workflow.ListDietaryRevisableAsync(h.Workspace)).Candidates).DocumentId);
        Assert.Equal(scaleId, Assert.Single((await h.Workflow.ListRevisableAsync(h.Workspace, h.Run)).Candidates).DocumentId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Workflow.PrepareDietaryAsync(h.Workspace, true, scaleId).AsTask());
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Workflow.PrepareRevisionAsync(h.Workspace, h.Run, dietaryId).AsTask());
        var old = await h.Workflow.ReadStoredAsync(dietaryId);
        h.Workspace.DietaryRecallSurvey!.RecallEntries[0].Weight = 150;
        h.Workspace.DietaryRecallSurvey.Calculate();
        var revised = await h.Workflow.PrepareDietaryAsync(h.Workspace, true, dietaryId);
        var stale = await h.Workflow.PrepareDietaryAsync(h.Workspace, true, dietaryId);
        Assert.Equal(dietaryId, await h.Workflow.IssueAsync(revised));
        await Assert.ThrowsAsync<InvalidDataException>(() => h.Workflow.IssueAsync(stale).AsTask());
        var current = await h.Workflow.ReadStoredAsync(dietaryId);
        Assert.Equal(2, current.Versions.Count);
        Assert.Equal(old.Pdf.ToArray(), current.PreviousPdfs[old.Report.Metadata.VersionId.Value].ToArray());
        Assert.True((await h.Archives.OpenStoredAsync(dietaryId)).Operation.IsSuccess);
    }

    [Fact]
    public async Task Dietary_panel_reuses_preview_failure_protection()
    {
        var h = DietaryHarness();
        var js = new PreviewRuntime { Failure = "createPreview" };
        using var panel = CreatePanel(h, js);
        ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(ReportPanel.IsDietary)] = true }).SetParameterProperties(panel);
        await InvokePanelAsync(panel, "PrepareAsync", true, null);
        await InvokePanelAsync(panel, "IssueAsync");
        Assert.Empty(h.Store.Documents);
        js.Failure = null;
        await InvokePanelAsync(panel, "PrepareAsync", true, null);
        await InvokePanelAsync(panel, "IssueAsync");
        Assert.Single(h.Store.Documents);
    }

    [Fact]
    public async Task Dietary_pdf_model_preserves_units_references_guidance_and_can_export_synthetic_fixture()
    {
        var h = DietaryHarness(includeLayoutDetails: true);
        var js = new DietaryPdfRuntime();
        var renderer = new PdfMakeDietaryReportRenderer(js);
        var factory = new DietaryReportFactory(new(new(new Uri("urn:test:dietary"), "膳食报告测试", "1")));
        var draft = factory.Create(h.Workspace, renderer.Template, h.Access.Actor, DateTimeOffset.UtcNow);
        await renderer.RenderAsync(draft);
        var json = js.Model!.Value;
        Assert.Equal(2, json.GetProperty("foods").GetArrayLength());
        Assert.Contains("150 g", json.GetRawText());
        Assert.Contains("60 g/d-", json.GetRawText());
        Assert.Contains("RNI-UL", json.GetRawText());
        Assert.Contains("AMDR_L", json.GetRawText());
        Assert.Contains("蔬菜类", json.GetRawText());
        Assert.False(json.GetProperty("isEvaluation").GetBoolean());
        Assert.True(js.Disposed);
        // 可选地向忽略目录导出合成模型，供真实 PDF 模板和视觉回归使用。
        if (Environment.GetEnvironmentVariable("EZNUTRITION_REPORT_TEST_OUTPUT") is { Length: > 0 } output)
        {
            Directory.CreateDirectory(output);
            await File.WriteAllTextAsync(Path.Combine(output, "dietary-model.json"), json.GetRawText());
        }
    }

    private static Harness DietaryHarness(bool includeLayoutDetails = false)
    {
        var h = new Harness();
        var nutrients = RuntimeArchiveSamples.CreateNutrients();
        var foods = RuntimeArchiveSamples.CreateFoods(nutrients, 29);
        if (includeLayoutDetails)
        {
            foreach (var (name, unit) in new[] { ("γ-生育酚", "mg"), ("水分", "g"), ("α-生育酚", "mg"), ("钙", "mg") })
            {
                var nutrient = new Nutrient { NutrientId = nutrients.Count + 1, FriendlyName = name, DefaultMeasureUnit = unit };
                nutrients.Add(nutrient);
                foreach (var food in foods) food.FoodNutrientValues!.Add(new()
                {
                    FoodId = food.FoodId, Food = food, NutrientId = nutrient.NutrientId, Nutrient = nutrient,
                    MeasureUnit = unit, Value = name == "水分" ? 50 : 1
                });
            }
        }
        var dris = new EzNutrition.Domain.Assessments.DRIs(h.Workspace.Client)
        {
            AvailableDRIs =
            [
                new() { Nutrient = "蛋白质", RecordType = DietaryReferenceIntakeType.RNI, Value = 60, MeasureUnit = "g/d" },
                new() { Nutrient = "蛋白质", RecordType = DietaryReferenceIntakeType.AMDR_L, Value = 10, MeasureUnit = "%E" },
                new() { Nutrient = "蛋白质", RecordType = DietaryReferenceIntakeType.AMDR_H, Value = 20, MeasureUnit = "%E" }
            ]
        };
        if (includeLayoutDetails) dris.AvailableDRIs = [.. dris.AvailableDRIs,
            new() { Nutrient = "蛋白质", RecordType = DietaryReferenceIntakeType.UL, Value = 150, MeasureUnit = "g/d" },
            new() { Nutrient = "VitA", RecordType = DietaryReferenceIntakeType.UL, Value = 3000, MeasureUnit = "μg" },
            new() { Nutrient = "VitE", RecordType = DietaryReferenceIntakeType.AI, Value = 14, MeasureUnit = "mg α-TE" }];
        var survey = new DietaryRecallSurvey(h.Workspace.Client, foods, nutrients, dris);
        survey.RecallEntries.Add(new() { Food = foods[0], Weight = 100, MealOccasion = MealOccasion.Breakfast });
        survey.RecallEntries.Add(new() { Food = foods[1], Weight = 200, IsAllEdible = false, MealOccasion = MealOccasion.Lunch });
        survey.Calculate();
        h.Workspace.DietaryRecallSurvey = survey;
        h.Workspace.DietaryTower = new ReportTower();
        return h;
    }

    private sealed class ReportTower : DietaryTower
    {
        public override TowerLayer[] RenderTower() => [new() { LayerName = "蔬菜类", DietaryRecallTower = "100 g", StandardTowerValue = "300-500 g" }];
    }

    private sealed class DietaryPdfRuntime : IJSRuntime, IJSObjectReference
    {
        public JsonElement? Model { get; private set; }
        public bool Disposed { get; private set; }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, default, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier == "import") return ValueTask.FromResult((TValue)(object)this);
            Model = JsonSerializer.SerializeToElement(args![0], new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            return ValueTask.FromResult((TValue)(object)"%PDF-1.7\nsynthetic"u8.ToArray());
        }
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }
}
