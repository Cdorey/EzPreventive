using EzNutrition.Application.Reports;
using Microsoft.JSInterop;

namespace EzNutrition.Client.Infrastructure;

/// <summary>打开浏览器本机 PDF 查看器，由使用者通过查看器选择打印机。</summary>
public sealed class BrowserReportPrinter(IJSRuntime js) : IReportPrinter, IAsyncDisposable
{
    private IJSObjectReference? module;

    /// <inheritdoc />
    public async ValueTask PrintAsync(ReadOnlyMemory<byte> pdf, string title, CancellationToken cancellationToken = default)
    {
        module ??= await js.InvokeAsync<IJSObjectReference>("import", cancellationToken, "./js/report-printing.mjs");
        await module.InvokeVoidAsync("openForPrinting", cancellationToken, pdf.ToArray());
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (module is not null) await module.DisposeAsync();
    }
}
