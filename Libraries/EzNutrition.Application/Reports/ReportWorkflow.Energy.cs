using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Application.Consultations;
using EzNutrition.Archives.Contracts.Resources;

namespace EzNutrition.Application.Reports;

public sealed partial class ReportWorkflow
{
    /// <summary>准备能量核算评估稿、正式初版或选定的更正版。</summary>
    public ValueTask<PreparedReport> PrepareEnergyAsync(ConsultationWorkspace workspace, bool forIssuance,
        Guid? revisionId = null, EnergyReportOptions? options = null, CancellationToken cancellationToken = default)
        => PrepareModuleAsync(forIssuance, revisionId,
            (signer, previous) => energyFactory.Create(workspace, energyRenderer.Template, signer, DateTimeOffset.UtcNow, previous, options),
            energyRenderer.RenderAsync, cancellationToken);

    /// <summary>列出本次咨询能量核算可更正的报告。</summary>
    public ValueTask<ReportRevisionList> ListEnergyRevisableAsync(ConsultationWorkspace workspace,
        CancellationToken cancellationToken = default) => ListRevisableAsync(workspace,
            workspace.ContractIdentity.EnergyAssessment.ResourceId, ArchiveResourceTypes.EnergyAssessment, cancellationToken);
}
