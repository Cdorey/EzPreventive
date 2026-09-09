using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Application.Consultations;
using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Archives.Contracts.ValueObjects;

namespace EzNutrition.Application.Reports;

public sealed partial class ReportWorkflow
{
    /// <summary>准备膳食调查评估稿、正式初版或明确选定的更正版。</summary>
    public async ValueTask<PreparedReport> PrepareDietaryAsync(ConsultationWorkspace workspace, bool forIssuance,
        Guid? revisionId = null, DietaryReportOptions? options = null, CancellationToken cancellationToken = default)
    {
        ActorReference? signer = null;
        if (forIssuance)
        {
            RequireStorage();
            signer = await authorization.RequireIssuerAsync(cancellationToken);
        }
        else
        {
            if (revisionId is not null) throw new InvalidOperationException("更正报告需要签发权限。");
            await authorization.RequirePrintAsync(cancellationToken);
        }
        SignedReport? previous = null;
        ReadOnlyMemory<byte>? expectedContent = null;
        if (revisionId is { } id)
        {
            var stored = await store.GetAsync(id, cancellationToken) ?? throw new FileNotFoundException("未找到要更正的报告。");
            previous = await DecodeStoredAsync(stored, cancellationToken);
            expectedContent = stored.Content;
        }
        var draft = dietaryFactory.Create(workspace, dietaryRenderer.Template, signer, DateTimeOffset.UtcNow, previous, options);
        var pdf = await dietaryRenderer.RenderAsync(draft, cancellationToken);
        _ = ReportPdf.Identity(pdf);
        return new PreparedReport(draft, pdf, expectedContent);
    }

    /// <summary>列出本次咨询膳食调查可更正的报告。</summary>
    public ValueTask<ReportRevisionList> ListDietaryRevisableAsync(ConsultationWorkspace workspace,
        CancellationToken cancellationToken = default) => ListRevisableAsync(workspace,
            workspace.ContractIdentity.DietaryRecall.ResourceId, ArchiveResourceTypes.DietaryRecall, cancellationToken);
}
