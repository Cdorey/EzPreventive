using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Archives.Contracts.Metadata;
using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Archives.Contracts.Serialization;
using EzNutrition.Archives.Contracts.ValueObjects;

namespace EzNutrition.Application.Reports;

/// <summary>组装各模块共用的报告身份、签发范围与修订来源。</summary>
internal static class ReportDraftAssembler
{
    private static readonly Uri PurposeSystem = new("https://eznutrition.cdorey.net/codes/report-purpose");
    private static readonly Uri ParticipationSystem = new("https://eznutrition.cdorey.net/codes/report-participation");

    public static ArchiveDocument Create(ArchiveDocument document, ResourceTypeCode resourceType, string title,
        CanonicalReference template, ActorReference? signer, DateTimeOffset capturedAt, SignedReport? previous)
    {
        var consultation = document.Bundle.Entries.OfType<ConsultationResource>().Single();
        var content = document.Bundle.Entries.Single(resource => resource.ResourceType == resourceType);
        if (previous is not null)
        {
            if (signer is null || previous.Document.ContainsUnknownContent)
                throw new InvalidOperationException("更正需要签发人，且旧报告不能包含当前版本无法解释的内容。");
            if (previous.Report.SubjectReference != consultation.SubjectReference
                || previous.Report.ConsultationReference.ResourceId != consultation.Metadata.ResourceId
                || !previous.Report.InputResourceReferences.Any(reference => reference.ResourceId == content.Metadata.ResourceId
                    && reference.ExpectedResourceType == content.ResourceType))
                throw new InvalidOperationException("只能更正同一患者、同一次咨询及同一份模块记录的报告。");
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
            Title = title,
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
        return document with
        {
            Bundle = document.Bundle with
            {
                Entries = document.Bundle.Entries.Select(resource => resource is ConsultationResource ? consultation : resource).Append(report).ToArray()
            }
        };
    }
}
