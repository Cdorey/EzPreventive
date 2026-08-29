using EzNutrition.Application.Archives;
using EzNutrition.Application.Consultations;
using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Archives.Contracts.ValueObjects;
using EzNutrition.Assessments.Common;
using EzNutrition.Domain.Consultations;

namespace EzNutrition.Application.Tests.Consultations;

/// <summary>
/// 验证通用量表运行态的重新计分和档案映射。
/// </summary>
public sealed class NutritionAssessmentApplicationServiceTests
{
    /// <summary>
    /// 验证宿主注册只形成可选目录，不会替任意咨询预先建立运行实例。
    /// </summary>
    [Fact]
    public void Registered_instruments_remain_a_catalog_until_selected()
    {
        var workspace = CreateWorkspace();
        var service = new NutritionAssessmentApplicationService([new Nrs2002Instrument()]);

        var definition = Assert.Single(service.Definitions);

        Assert.Equal("nrs-2002", definition.Code);
        Assert.Empty(workspace.NutritionAssessments);
    }

    /// <summary>
    /// 验证通用实现库中的量表均可进入按需新增目录，且不会预先创建任何量表实例。
    /// </summary>
    [Fact]
    public void Common_instruments_form_the_complete_on_demand_catalog()
    {
        var workspace = CreateWorkspace();
        var service = new NutritionAssessmentApplicationService(
        [
            new Nrs2002Instrument(),
            new MnaSfInstrument(),
            new MustInstrument(),
            new WsT552ElderlyMalnutritionRiskInstrument(),
            new SgaInstrument(),
            new ChasSgaInstrument(),
            new PgSgaInstrument()
        ]);

        Assert.Equal(
        [
            "nrs-2002",
            "mna-sf",
            "must",
            "ws-t-552-elderly-malnutrition-risk",
            "sga",
            "sga-chas-2020",
            "pg-sga"
        ],
            service.Definitions.Select(definition => definition.Code));
        Assert.Empty(workspace.NutritionAssessments);
    }

    /// <summary>
    /// 验证同一代码体系、量表编码和版本不能被宿主重复注册。
    /// </summary>
    [Fact]
    public void Duplicate_instrument_registration_is_rejected()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new NutritionAssessmentApplicationService(
                [new Nrs2002Instrument(), new Nrs2002Instrument()]));
    }

    /// <summary>
    /// 验证营养状况小结改变后，运行态重新取三个小结中的最高分。
    /// </summary>
    [Fact]
    public void Changing_nutritional_subscore_recomputes_the_highest_score()
    {
        var workspace = CreateWorkspace();
        var run = CreateRun(workspace);
        CompleteScreening(
            run,
            weightLoss: "over-five-percent-within-two-months",
            intakeReduction: "reduced-25-to-50-percent");

        Assert.True(run.Evaluation.IsComplete);
        Assert.Equal(3m, run.Evaluation.TotalScore);
        Assert.Equal(4, run.Answers.Count);

        run.SetAnswer("recent-weight-loss", "no-scored-weight-loss");

        Assert.True(run.Evaluation.IsComplete);
        Assert.Equal(2m, run.Evaluation.TotalScore);
        Assert.Equal("no-current-nutritional-risk", run.Evaluation.Interpretation?.Code);
        Assert.Equal(4, run.Answers.Count);
    }

    /// <summary>
    /// 验证同一量表在当前咨询中只能保留一个活动运行实例。
    /// </summary>
    [Fact]
    public void Starting_an_already_open_instrument_is_rejected()
    {
        var workspace = CreateWorkspace();
        var service = new NutritionAssessmentApplicationService([new Nrs2002Instrument()]);
        var definition = Assert.Single(service.Definitions);
        service.StartRun(workspace, definition, workspace.ContractIdentity.CreatedAt);

        Assert.Throws<InvalidOperationException>(() =>
            service.StartRun(workspace, definition, workspace.ContractIdentity.CreatedAt));
        Assert.Single(workspace.NutritionAssessments);
    }

    /// <summary>
    /// 验证通用组装器将具体 NRS 运行结果映射到既有通用量表档案资源。
    /// </summary>
    [Fact]
    public void Completed_run_maps_to_generic_scale_archive_resource()
    {
        var workspace = CreateWorkspace();
        var run = CreateRun(workspace);
        CompleteScreening(
            run,
            weightLoss: "over-five-percent-within-three-months",
            diseaseSeverity: "mild");
        var assembler = new ArchiveContractAssembler(new ApplicationIdentity(
            new Uri("https://eznutrition.cdorey.net/applications/assessment-test"),
            "量表集成测试",
            "2.1-test"));

        var document = assembler.CreateDocument(
            workspace,
            workspace.ContractIdentity.CreatedAt.AddMinutes(5));
        var resource = Assert.Single(
            document.Bundle.Entries.OfType<NutritionScaleAssessmentResource>());

        Assert.Equal("nrs-2002", resource.Instrument.Code.Code);
        Assert.Equal("WS/T 427—2013", resource.Instrument.Version);
        Assert.Equal(3m, resource.TotalScore);
        Assert.Equal("nrs-2002/interpretation/nutritional-risk", resource.Interpretation?.Code);
        Assert.Equal(4, resource.Responses.Count);
        Assert.Equal(6, resource.DerivedResults.Count);
        Assert.Equal(run.ArchiveIdentity.ResourceId, resource.Metadata.ResourceId);
    }

    /// <summary>
    /// 验证关闭量表会删除运行态回答并阻止归档，但不追溯删除已经确认的 SOAP 文本。
    /// </summary>
    [Fact]
    public void Removing_run_excludes_it_from_archive_without_rewriting_soap()
    {
        var workspace = CreateWorkspace();
        workspace.SubjectiveObjectiveAssessmentPlanInformation = new()
        {
            Objective = "已由专业人员确认的量表摘要"
        };
        var service = new NutritionAssessmentApplicationService([new Nrs2002Instrument()]);
        var run = service.StartRun(
            workspace,
            Assert.Single(service.Definitions),
            workspace.ContractIdentity.CreatedAt);
        CompleteScreening(run);

        var removed = service.RemoveRun(workspace, run.RunId);
        var document = new ArchiveContractAssembler(new ApplicationIdentity(
                new Uri("https://eznutrition.cdorey.net/applications/assessment-removal-test"),
                "量表移除测试",
                "2.1-test"))
            .CreateDocument(workspace, workspace.ContractIdentity.CreatedAt.AddMinutes(5));

        Assert.True(removed);
        Assert.Empty(workspace.NutritionAssessments);
        Assert.DoesNotContain(
            document.Bundle.Entries,
            entry => entry is NutritionScaleAssessmentResource);
        Assert.Equal(
            "已由专业人员确认的量表摘要",
            workspace.SubjectiveObjectiveAssessmentPlanInformation.Objective);
    }

    private static ConsultationWorkspace CreateWorkspace() => new(new ClientInfo
    {
        Gender = "女",
        Age = new EzNutrition.Domain.Consultations.ChronologicalAge(70),
        Height = 165m,
        Weight = 60m
    });

    private static NutritionAssessmentRun CreateRun(ConsultationWorkspace workspace)
    {
        var service = new NutritionAssessmentApplicationService([new Nrs2002Instrument()]);
        return service.StartRun(
            workspace,
            Assert.Single(service.Definitions),
            workspace.ContractIdentity.CreatedAt);
    }

    private static void CompleteScreening(
        NutritionAssessmentRun run,
        string bmiStatus = "bmi-at-least-18-5",
        string weightLoss = "no-scored-weight-loss",
        string intakeReduction = "no-scored-intake-reduction",
        string diseaseSeverity = "no-scored-disease-severity")
    {
        run.SetAnswer("bmi-status", bmiStatus);
        run.SetAnswer("recent-weight-loss", weightLoss);
        run.SetAnswer("last-week-intake-reduction", intakeReduction);
        run.SetAnswer("disease-severity", diseaseSeverity);
    }
}
