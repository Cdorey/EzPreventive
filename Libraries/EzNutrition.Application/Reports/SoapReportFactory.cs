using EzNutrition.Application.Archives;
using EzNutrition.Application.Consultations;
using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Archives.Contracts.Serialization;
using EzNutrition.Archives.Contracts.ValueObjects;
using EzNutrition.Domain.Consultations;

namespace EzNutrition.Application.Reports;

/// <summary>捕获咨询中的 SOAP 记录，供本机审核和打印。</summary>
public sealed class SoapReportFactory(ArchiveContractAssembler assembler)
{
    /// <summary>SOAP 四节中至少一节有实际内容。</summary>
    public static bool HasContent(SubjectiveObjectiveAssessmentPlanInformation? note) => note is not null
        && new[] { note.Subjective, note.Objective, note.Assessment, note.Plan }.Any(value => !string.IsNullOrWhiteSpace(value));

    /// <summary>创建独立的患者、咨询与 SOAP 输入快照。</summary>
    public SoapReportDraft Create(ConsultationWorkspace workspace, CanonicalReference template,
        ActorReference? signer, DateTimeOffset capturedAt, SignedReport? previous = null)
    {
        if (!HasContent(workspace.SubjectiveObjectiveAssessmentPlanInformation))
            throw new InvalidOperationException("请先填写至少一节 SOAP 记录，再生成报告。");
        if (string.IsNullOrWhiteSpace(template.Version)) throw new ArgumentException("报告模板必须声明版本。", nameof(template));
        if (signer is not null && (string.IsNullOrWhiteSpace(workspace.Client.Name)
            || signer.Identifier is null || string.IsNullOrWhiteSpace(signer.Display) || signer.AbsentReason is not null))
            throw new InvalidOperationException("正式报告需要患者姓名及完整的签发人身份。");
        return new SoapReportDraft(ReportDraftAssembler.Create(assembler.CreateSoapDocument(workspace, capturedAt),
            ArchiveResourceTypes.SoapNote, "SOAP 咨询记录报告", template, signer, capturedAt, previous), signer, previous, workspace);
    }
}

/// <summary>保存 SOAP 输入快照，并检查审核期间记录与患者资料的变化。</summary>
public sealed class SoapReportDraft : ReportDraft
{
    private readonly ConsultationWorkspace workspace;
    private readonly SubjectiveObjectiveAssessmentPlanInformation note;
    private readonly object state;

    internal SoapReportDraft(ArchiveDocument document, ActorReference? signer, SignedReport? previous,
        ConsultationWorkspace workspace) : base(document, signer, previous)
    {
        this.workspace = workspace;
        note = workspace.SubjectiveObjectiveAssessmentPlanInformation!;
        state = State(workspace, note);
    }

    /// <inheritdoc />
    public override void EnsureCurrent()
    {
        if (!ReferenceEquals(workspace.SubjectiveObjectiveAssessmentPlanInformation, note) || !state.Equals(State(workspace, note)))
            throw new InvalidOperationException("SOAP 记录或患者资料已变化，请重新生成并审核报告。");
    }

    private static object State(ConsultationWorkspace workspace, SubjectiveObjectiveAssessmentPlanInformation note)
    {
        var client = workspace.Client;
        return ((client.Name, client.Gender, client.Age, client.BirthDate, client.Height, client.Weight, client.SpecialPhysiologicalPeriod),
            (note.Subjective, note.Objective, note.Assessment, note.Plan));
    }
}

/// <summary>由 Blazor 宿主共用的 SOAP PDF 渲染端口。</summary>
public interface ISoapReportRenderer
{
    /// <summary>模板身份和确切版本。</summary>
    CanonicalReference Template { get; }
    /// <summary>渲染固定快照。</summary>
    ValueTask<byte[]> RenderAsync(SoapReportDraft draft, CancellationToken cancellationToken = default);
}
