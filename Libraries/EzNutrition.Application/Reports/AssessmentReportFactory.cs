using EzNutrition.Application.Archives;
using EzNutrition.Application.Consultations;
using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Archives.Contracts.Metadata;
using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Archives.Contracts.ValueObjects;

namespace EzNutrition.Application.Reports;

/// <summary>从当前咨询捕获单份量表报告，复用已有档案映射，不在模板里重新解释量表规则。</summary>
public sealed class AssessmentReportFactory(ArchiveContractAssembler assembler)
{
    private static readonly Uri PurposeSystem = new("https://eznutrition.cdorey.net/codes/report-purpose");
    private static readonly Uri ParticipationSystem = new("https://eznutrition.cdorey.net/codes/report-participation");

    /// <summary>
    /// 创建独立预览；正式签发需完整量表、明确患者及具有稳定身份和姓名的签发人。
    /// </summary>
    /// <param name="workspace">当前咨询。</param>
    /// <param name="assessment">要输出的量表。</param>
    /// <param name="template">渲染器提供的模板标识与版本。</param>
    /// <param name="signer">通过现有权限检查后取得的拟签发人；评估输出传空。</param>
    /// <param name="capturedAt">预览中显示的报告时间。</param>
    /// <param name="previous">同一量表报告的当前正式版本；为空时建立初版。</param>
    public AssessmentReportDraft Create(
        ConsultationWorkspace workspace,
        NutritionAssessmentRun assessment,
        CanonicalReference template,
        ActorReference? signer,
        DateTimeOffset capturedAt,
        SignedReport? previous = null)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(assessment);
        ArgumentNullException.ThrowIfNull(template);
        if (string.IsNullOrWhiteSpace(template.Version))
        {
            throw new ArgumentException("报告模板必须声明确切版本。", nameof(template));
        }

        if (signer is not null)
        {
            if (!assessment.Evaluation.IsComplete)
            {
                throw new InvalidOperationException("请完成量表后再签发报告。");
            }

            if (string.IsNullOrWhiteSpace(workspace.Client.Name))
            {
                throw new InvalidOperationException("正式报告需要明确的患者姓名。");
            }

            if (signer.Identifier is null || string.IsNullOrWhiteSpace(signer.Display) || signer.AbsentReason is not null)
            {
                throw new InvalidOperationException("签发人的稳定身份或姓名不完整。");
            }
        }

        var document = assembler.CreateAssessmentDocument(workspace, assessment, capturedAt);
        var consultation = document.Bundle.Entries.OfType<ConsultationResource>().Single();
        var scale = document.Bundle.Entries.OfType<NutritionScaleAssessmentResource>().Single();
        if (previous is not null)
        {
            if (signer is null || previous.Document.ContainsUnknownContent)
                throw new InvalidOperationException("更正需要签发人，且旧报告不能包含当前版本无法解释的内容。");
            if (previous.Report.SubjectReference != consultation.SubjectReference
                || previous.Report.ConsultationReference.ResourceId != consultation.Metadata.ResourceId
                || !previous.Report.InputResourceReferences.Any(reference => reference.ResourceId == scale.Metadata.ResourceId
                    && reference.ExpectedResourceType == scale.ResourceType))
                throw new InvalidOperationException("只能更正同一患者、同一次咨询及同一次量表评估的报告。");
            if (capturedAt < previous.Report.Metadata.FinalizedAt)
                throw new InvalidOperationException("当前时间早于旧报告签发时间，请核对设备时间后重试。");
        }
        var report = new NutritionReportResource
        {
            Metadata = new ResourceMetadata
            {
                ResourceId = previous?.Report.Metadata.ResourceId ?? new ResourceId(Guid.NewGuid()),
                VersionId = new ResourceVersionId(Guid.NewGuid()),
                RevisionNumber = new RevisionNumber((previous?.Report.Metadata.RevisionNumber.Value ?? 0) + 1),
                BasedOn = previous is null ? null : new VersionedResourceReference(
                    previous.Report.Metadata.ResourceId, previous.Report.Metadata.VersionId, previous.Report.ResourceType),
                Status = ResourceLifecycleStatus.Draft,
                CreatedAt = capturedAt,
                LastModifiedAt = capturedAt,
                SourceApplication = document.Bundle.Producer
            },
            SubjectReference = consultation.SubjectReference,
            ConsultationReference = new VersionedResourceReference(
                consultation.Metadata.ResourceId, consultation.Metadata.VersionId, consultation.ResourceType),
            Purpose = new Coding(PurposeSystem, signer is null ? "evaluation" : "clinical",
                display: signer is null ? "教学或功能评估" : "医疗服务"),
            Title = $"{assessment.Definition.DisplayName}报告",
            InputResourceReferences = document.Bundle.Entries.Select(resource => new VersionedResourceReference(
                resource.Metadata.ResourceId, resource.Metadata.VersionId, resource.ResourceType)).ToArray(),
            PresentationTemplate = template,
            Participants = signer is null ? [] :
            [new ReportParticipation
            {
                Function = new Coding(ParticipationSystem, "reviewer", display: "审核人"),
                Actor = signer,
                ActedAt = capturedAt
            }]
        };
        consultation = consultation with
        {
            ClinicalResourceReferences = [.. consultation.ClinicalResourceReferences,
                new VersionedResourceReference(report.Metadata.ResourceId, report.Metadata.VersionId, report.ResourceType)]
        };
        return new AssessmentReportDraft(document with
        {
            Bundle = document.Bundle with
            {
                Entries = [document.Bundle.Entries.OfType<PatientResource>().Single(), consultation, scale, report]
            }
        }, signer, previous);
    }
}
