using EzNutrition.Archives.Contracts.Abstractions;
using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Archives.Contracts.Serialization;
using EzNutrition.Archives.Contracts.ValueObjects;

namespace EzNutrition.Application.Archives;

/// <summary>从契约资源提取已记录的历史事实；不推断缺失结果，不使用普通调阅文案反向解析数据。</summary>
internal static class ConsultationHistoryProjector
{
    public static ConsultationHistoryEntry? Create(
        ArchiveDocument document, Guid patientId, Guid documentId, DateTimeOffset savedAt)
    {
        var patients = document.Bundle.Entries.OfType<PatientResource>().ToArray();
        var consultations = document.Bundle.Entries.OfType<ConsultationResource>().ToArray();
        if (patients.Length != 1 || patients[0].Metadata.ResourceId.Value != patientId ||
            consultations.Length != 1 || consultations[0].SubjectReference.ResourceId.Value != patientId)
            return null;

        var consultation = consultations[0];
        var facts = new List<ConsultationHistoryFact>();
        Measurement(ConsultationHistoryItem.Height, consultation.SubjectSnapshot?.Height);
        Measurement(ConsultationHistoryItem.Weight, consultation.SubjectSnapshot?.Weight);

        foreach (var resource in document.Bundle.Entries)
        {
            switch (resource)
            {
                case EnergyAssessmentResource energy:
                    CheckOwnership(energy, energy.SubjectReference, energy.ConsultationReference);
                    if (energy.ProfessionalDecision is { } decision)
                    {
                        QuantityFact(ConsultationHistoryItem.AdoptedEnergy, decision.AdoptedEnergyTarget, energy.EffectiveAt);
                    }
                    break;
                case DietaryRecallResource recall:
                    CheckOwnership(recall, recall.SubjectReference, recall.ConsultationReference);
                    if (recall.EnergyConsistency is { } consistency)
                        QuantityFact(ConsultationHistoryItem.DietaryEnergy, consistency.RecordedTotalEnergy, recall.RecallPeriod?.Start);
                    break;
                case SoapNoteResource soap:
                    CheckOwnership(soap, soap.SubjectReference, soap.ConsultationReference);
                    TextFact(ConsultationHistoryItem.Subjective, soap.Subjective, soap.EffectiveAt);
                    TextFact(ConsultationHistoryItem.Objective, soap.Objective, soap.EffectiveAt);
                    TextFact(ConsultationHistoryItem.Assessment, soap.Assessment, soap.EffectiveAt);
                    TextFact(ConsultationHistoryItem.Plan, soap.Plan, soap.EffectiveAt);
                    break;
                case NutritionAdviceResource advice:
                    CheckOwnership(advice, advice.SubjectReference, advice.ConsultationReference);
                    // 失败、进行中或中断的生成不能当成已完成的既往建议。
                    if (advice.GenerationStatus == NutritionAdviceGenerationStatus.Completed)
                        TextFact(ConsultationHistoryItem.Advice, advice.NarrativeContent, advice.CompletedAt ?? advice.RequestedAt);
                    break;
            }
        }

        return new(documentId, consultation.Metadata.ResourceId.Value, consultation.Period.Start,
            savedAt, Array.AsReadOnly(facts.ToArray()));

        void Measurement(ConsultationHistoryItem item, ClinicalMeasurement? measurement)
        {
            if (measurement is not null) QuantityFact(item, measurement.Value, measurement.EffectiveAt);
        }

        void QuantityFact(ConsultationHistoryItem item, Quantity quantity, DateTimeOffset? effectiveAt)
        {
            var comparator = quantity.Comparator switch
            {
                QuantityComparator.LessThan => "<",
                QuantityComparator.LessThanOrEqual => "≤",
                QuantityComparator.GreaterThan => ">",
                QuantityComparator.GreaterThanOrEqual => "≥",
                _ => string.Empty
            };
            facts.Add(new(item, effectiveAt ?? consultation.Period.Start, effectiveAt is null,
                new(quantity.Value, quantity.Unit.System.AbsoluteUri, quantity.Unit.Code,
                    quantity.Unit.Display ?? quantity.Unit.Code, comparator), null));
        }

        void TextFact(ConsultationHistoryItem item, string? text, DateTimeOffset? effectiveAt)
        {
            if (!string.IsNullOrWhiteSpace(text))
                facts.Add(new(item, effectiveAt ?? consultation.Period.Start, effectiveAt is null, null, text));
        }

        void CheckOwnership(IArchiveResource resource, LogicalResourceReference subject, VersionedResourceReference? parent)
        {
            var listed = consultation.ClinicalResourceReferences.Any(reference =>
                reference.ResourceId == resource.Metadata.ResourceId && reference.VersionId == resource.Metadata.VersionId);
            if (subject.ResourceId.Value != patientId || !listed ||
                parent is not null && (parent.ResourceId != consultation.Metadata.ResourceId || parent.VersionId != consultation.Metadata.VersionId))
                throw new InvalidDataException("历史资源与患者或咨询引用不一致。");
        }
    }
}
