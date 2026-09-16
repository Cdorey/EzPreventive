using EzNutrition.Archives.Contracts.ValueObjects;

namespace EzNutrition.Application.Reports;

public sealed partial class ReportWorkflow
{
    private async ValueTask<PreparedReport> PrepareModuleAsync<TDraft>(bool forIssuance, Guid? revisionId,
        Func<ActorReference?, SignedReport?, TDraft> capture,
        Func<TDraft, CancellationToken, ValueTask<byte[]>> render, CancellationToken cancellationToken)
        where TDraft : ReportDraft
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
        var draft = capture(signer, previous);
        var pdf = await render(draft, cancellationToken);
        _ = ReportPdf.Identity(pdf);
        return new PreparedReport(draft, pdf, expectedContent);
    }
}
