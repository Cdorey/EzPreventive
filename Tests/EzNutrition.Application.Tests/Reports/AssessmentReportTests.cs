using EzNutrition.Application.Archives;
using EzNutrition.Application.Consultations;
using EzNutrition.Application.Reports;
using EzNutrition.Archives.Contracts.Metadata;
using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Archives.Contracts.Validation;
using EzNutrition.Archives.Contracts.ValueObjects;
using EzNutrition.Assessments.Common;
using EzNutrition.Domain.Consultations;

namespace EzNutrition.Application.Tests.Reports;

/// <summary>验证报告快照、签发范围和原件绑定，不依赖 PDF 库或宿主。</summary>
public sealed class AssessmentReportTests
{
    private static readonly CanonicalReference Template = new(new Uri("urn:test:assessment-report"), "1");
    private static readonly ActorReference Physician = new()
    {
        Identifier = new BusinessIdentifier(new Uri("urn:test:users"), "physician-1"),
        Display = "测试医师"
    };

    /// <summary>报告独立版本和回答不会被工作区后续修改或草稿保存覆盖。</summary>
    [Fact]
    public void Report_captures_only_selected_assessment_with_independent_versions()
    {
        var (workspace, run, factory, assembler) = Scenario();
        var other = new NutritionAssessmentApplicationService([new MnaSfInstrument()]);
        other.StartRun(workspace, other.Definitions.Single());
        var draft = factory.Create(workspace, run, Template, Physician, DateTimeOffset.UtcNow);
        var originalScore = draft.Assessment.TotalScore;
        run.SetAnswer("bmi-score", "below-18-5");
        var current = assembler.CreateDocument(workspace);

        Assert.Equal(4, draft.Document.Bundle.Entries.Count);
        Assert.Single(draft.Document.Bundle.Entries.OfType<NutritionScaleAssessmentResource>());
        Assert.Equal(0m, originalScore);
        Assert.Equal(originalScore, draft.Assessment.TotalScore);
        Assert.Equal(2m, run.Evaluation.TotalScore);
        Assert.NotEqual(run.ArchiveIdentity.VersionId, draft.Assessment.Metadata.VersionId);
        Assert.NotEqual(workspace.ContractIdentity.Consultation.VersionId,
            draft.Report.ConsultationReference.VersionId);
        Assert.Equal(workspace.ContractIdentity.Consultation.ResourceId,
            draft.Report.ConsultationReference.ResourceId);
        Assert.DoesNotContain(current.Bundle.Entries, resource => resource is NutritionReportResource);
    }

    /// <summary>报告显示实际评分时的年龄和测量输入，不采用后来编辑的对象信息。</summary>
    [Fact]
    public void Report_preserves_the_subject_used_by_the_assessment()
    {
        var (workspace, run, factory, _) = Scenario();
        ((ClientInfo)workspace.Client).Weight = 90;
        var draft = factory.Create(workspace, run, Template, Physician, DateTimeOffset.UtcNow);
        var subject = draft.Document.Bundle.Entries.OfType<ConsultationResource>().Single().SubjectSnapshot!;
        Assert.Equal(60m, subject.Weight!.Value.Value);
        Assert.Equal(run.Subject.AgeInYears, subject.ChronologicalAgeAtConsultation!.Years);
    }

    /// <summary>正式签发满足契约引用闭包，且只将报告标为正式确认。</summary>
    [Fact]
    public void Signing_binds_exact_pdf_without_finalizing_the_entire_consultation()
    {
        var (workspace, run, factory, _) = Scenario();
        var draft = factory.Create(workspace, run, Template, Physician, DateTimeOffset.UtcNow);
        // 这里验证字节绑定而非 PDF 排版；实际 PDF 的渲染另做端到端验证。
        var pdf = "%PDF-1.7\nreport fixture"u8.ToArray();
        var signed = draft.BindSignedPdf(pdf, new ArchiveContractValidator());
        var report = signed.Bundle.Entries.OfType<NutritionReportResource>().Single();

        Assert.Equal(ResourceLifecycleStatus.Final, report.Metadata.Status);
        Assert.Equal(Physician, report.Metadata.FinalizedBy);
        Assert.Equal(draft.Report.Metadata.CreatedAt, report.Metadata.FinalizedAt);
        Assert.All(signed.Bundle.Entries.Where(resource => resource is not NutritionReportResource),
            resource => Assert.Equal(ResourceLifecycleStatus.Draft, resource.Metadata.Status));
        ReportPdf.Verify(pdf, report.RenderedArtifact!);
        Assert.Throws<InvalidDataException>(() =>
            ReportPdf.Verify("%PDF-1.7\nmodified"u8, report.RenderedArtifact!));
        Assert.Equal(ResourceLifecycleStatus.Draft, draft.Report.Metadata.Status);
        Assert.Null(draft.Report.RenderedArtifact);
    }

    /// <summary>未完成量表可产生评估稿，但不能借评估稿入口签发。</summary>
    [Fact]
    public void Incomplete_assessment_allows_evaluation_only()
    {
        var (workspace, run, factory, _) = Scenario();
        run.ClearAnswer("bmi-score");
        Assert.Throws<InvalidOperationException>(() =>
            factory.Create(workspace, run, Template, Physician, DateTimeOffset.UtcNow));
        var draft = factory.Create(workspace, run, Template, null, DateTimeOffset.UtcNow);
        Assert.Equal("evaluation", draft.Report.Purpose.Code);
        Assert.Contains(draft.Assessment.Responses, response => response.Answer is null
            && response.AnswerAbsentReason == DataAbsentReasonCode.NotAsked);
        Assert.Throws<InvalidOperationException>(() =>
            draft.BindSignedPdf("%PDF-1.7"u8, new ArchiveContractValidator()));
    }

    /// <summary>不能把其他咨询的量表或匿名咨询误当作当前患者的正式报告。</summary>
    [Fact]
    public void Signing_rejects_unidentified_patient_and_unrelated_assessment()
    {
        var (workspace, run, factory, _) = Scenario();
        var (another, _, _, _) = Scenario();
        Assert.Throws<ArgumentException>(() =>
            factory.Create(another, run, Template, Physician, DateTimeOffset.UtcNow));
        ((ClientInfo)workspace.Client).Name = " ";
        Assert.Throws<InvalidOperationException>(() =>
            factory.Create(workspace, run, Template, Physician, DateTimeOffset.UtcNow));
    }

    private static (ConsultationWorkspace, NutritionAssessmentRun, AssessmentReportFactory, ArchiveContractAssembler) Scenario()
    {
        var workspace = new ConsultationWorkspace(new ClientInfo
        {
            Name = "模拟患者", Gender = "女", Age = new EzNutrition.Domain.Consultations.ChronologicalAge(70), Height = 165, Weight = 60
        });
        var service = new NutritionAssessmentApplicationService([new MustInstrument()]);
        var run = service.StartRun(workspace, service.Definitions.Single());
        run.SetAnswer("bmi-score", "above-20");
        run.SetAnswer("unplanned-weight-loss", "below-five-percent");
        run.SetAnswer("acute-disease-effect", "absent");
        var assembler = new ArchiveContractAssembler(new ApplicationIdentity(
            new Uri("urn:test:report-app"), "报告测试", "1"));
        return (workspace, run, new AssessmentReportFactory(assembler), assembler);
    }
}
