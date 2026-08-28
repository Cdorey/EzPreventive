using EzNutrition.Application.Archives;
using EzNutrition.Application.Consultations;
using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Archives.Contracts.ValueObjects;
using EzNutrition.Assessments.Nrs2002;
using EzNutrition.Domain.Consultations;

namespace EzNutrition.Application.Tests.Consultations;

/// <summary>
/// 验证通用量表运行态的分支清理和档案映射。
/// </summary>
public sealed class NutritionAssessmentApplicationServiceTests
{
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
    /// 验证上游答案改变后，不再适用的终筛答案不会残留在工作区。
    /// </summary>
    [Fact]
    public void Changing_initial_path_discards_inapplicable_final_answers()
    {
        var workspace = CreateWorkspace();
        var run = CreateRun(workspace);
        CompleteInitialScreen(run, bmiBelow205: true);
        run.SetAnswer("impaired-nutritional-status", "3");
        run.SetAnswer("disease-severity", "2");
        Assert.True(run.Evaluation.IsComplete);
        Assert.Equal(6, run.Answers.Count);

        run.SetAnswer("initial-bmi-below-20-5", "no");

        Assert.True(run.Evaluation.IsComplete);
        Assert.Equal("negative-initial-screening", run.Evaluation.Interpretation?.Code);
        Assert.Equal(4, run.Answers.Count);
        Assert.DoesNotContain("impaired-nutritional-status", run.Answers.Keys);
        Assert.DoesNotContain("disease-severity", run.Answers.Keys);
    }

    /// <summary>
    /// 验证通用组装器将具体 NRS 运行结果映射到既有通用量表档案资源。
    /// </summary>
    [Fact]
    public void Completed_run_maps_to_generic_scale_archive_resource()
    {
        var workspace = CreateWorkspace();
        var run = CreateRun(workspace);
        CompleteInitialScreen(run, bmiBelow205: true);
        run.SetAnswer("impaired-nutritional-status", "1");
        run.SetAnswer("disease-severity", "1");
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
        Assert.Equal("2002", resource.Instrument.Version);
        Assert.Equal(3m, resource.TotalScore);
        Assert.Equal("nrs-2002/interpretation/nutritional-risk", resource.Interpretation?.Code);
        Assert.Equal(6, resource.Responses.Count);
        Assert.Equal(3, resource.DerivedResults.Count);
        Assert.Equal(run.ArchiveIdentity.ResourceId, resource.Metadata.ResourceId);
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
        service.EnsureRuns(workspace, workspace.ContractIdentity.CreatedAt);
        return Assert.Single(workspace.NutritionAssessments);
    }

    private static void CompleteInitialScreen(
        NutritionAssessmentRun run,
        bool bmiBelow205)
    {
        run.SetAnswer("initial-bmi-below-20-5", bmiBelow205 ? "yes" : "no");
        run.SetAnswer("initial-weight-loss-within-three-months", "no");
        run.SetAnswer("initial-reduced-intake-last-week", "no");
        run.SetAnswer("initial-severe-illness", "no");
    }
}
