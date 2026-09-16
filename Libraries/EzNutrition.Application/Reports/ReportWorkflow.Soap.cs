using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Application.Consultations;

namespace EzNutrition.Application.Reports;

public sealed partial class ReportWorkflow
{
    /// <summary>准备 SOAP 评估稿、正式初版或选定的更正版。</summary>
    public ValueTask<PreparedReport> PrepareSoapAsync(ConsultationWorkspace workspace, bool forIssuance,
        Guid? revisionId = null, CancellationToken cancellationToken = default)
        => PrepareModuleAsync(forIssuance, revisionId,
            (signer, previous) => soapFactory.Create(workspace, soapRenderer.Template, signer, DateTimeOffset.UtcNow, previous),
            soapRenderer.RenderAsync, cancellationToken);

    /// <summary>列出本次咨询 SOAP 可更正的报告。</summary>
    public ValueTask<ReportRevisionList> ListSoapRevisableAsync(ConsultationWorkspace workspace,
        CancellationToken cancellationToken = default) => ListRevisableAsync(workspace,
            workspace.ContractIdentity.SoapNote.ResourceId, ArchiveResourceTypes.SoapNote, cancellationToken);
}
