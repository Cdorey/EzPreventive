using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Archives.Contracts.Serialization;
using EzNutrition.Archives.Contracts.ValueObjects;

namespace EzNutrition.Application.Reports;

/// <summary>保存单份量表的报告预览快照。</summary>
public sealed class AssessmentReportDraft : ReportDraft
{
    internal AssessmentReportDraft(ArchiveDocument document, ActorReference? signer, SignedReport? previous = null)
        : base(document, signer, previous) { }

    /// <summary>获取本次量表结果。</summary>
    public NutritionScaleAssessmentResource Assessment =>
        Document.Bundle.Entries.OfType<NutritionScaleAssessmentResource>().Single();
}
