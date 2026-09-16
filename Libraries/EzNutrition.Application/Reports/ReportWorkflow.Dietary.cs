using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Application.Consultations;
using EzNutrition.Archives.Contracts.Resources;

namespace EzNutrition.Application.Reports;

public sealed partial class ReportWorkflow
{
    /// <summary>准备膳食调查评估稿、正式初版或明确选定的更正版。</summary>
    public ValueTask<PreparedReport> PrepareDietaryAsync(ConsultationWorkspace workspace, bool forIssuance,
        Guid? revisionId = null, DietaryReportOptions? options = null, CancellationToken cancellationToken = default)
        => PrepareModuleAsync(forIssuance, revisionId,
            (signer, previous) => dietaryFactory.Create(workspace, dietaryRenderer.Template, signer, DateTimeOffset.UtcNow, previous, options),
            dietaryRenderer.RenderAsync, cancellationToken);

    /// <summary>列出本次咨询膳食调查可更正的报告。</summary>
    public ValueTask<ReportRevisionList> ListDietaryRevisableAsync(ConsultationWorkspace workspace,
        CancellationToken cancellationToken = default) => ListRevisableAsync(workspace,
            workspace.ContractIdentity.DietaryRecall.ResourceId, ArchiveResourceTypes.DietaryRecall, cancellationToken);
}
