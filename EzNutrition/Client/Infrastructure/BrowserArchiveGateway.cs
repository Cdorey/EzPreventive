using EzNutrition.Application.Archives;
using Microsoft.JSInterop;

namespace EzNutrition.Client.Infrastructure;

/// <summary>
/// 通过浏览器 IndexedDB 和文件交互能力提供本机档案适配。
/// </summary>
public sealed class BrowserArchiveGateway(IJSRuntime jsRuntime) :
    IArchiveDocumentStore,
    IArchiveDocumentTransport,
    IAsyncDisposable
{
    private const int MaximumExternalDocumentBytes = 16 * 1024 * 1024;
    private readonly SemaphoreSlim moduleLock = new(1, 1);
    private IJSObjectReference? module;

    /// <inheritdoc />
    public ArchiveDocumentStoreCapabilities Capabilities =>
        ArchiveDocumentStoreCapabilities.Save | ArchiveDocumentStoreCapabilities.Browse;

    /// <inheritdoc />
    public bool CanOpen => true;

    /// <inheritdoc />
    public bool CanSave => true;

    /// <inheritdoc />
    public async ValueTask SaveAsync(
        StoredArchiveDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        var browserModule = await GetModuleAsync(cancellationToken);
        try
        {
            await browserModule.InvokeVoidAsync(
                "saveDocument",
                cancellationToken,
                BrowserStoredArchiveInfo.From(document.Info),
                document.Content.ToArray());
        }
        catch (JSException exception)
        {
            throw new IOException("浏览器无法保存本机档案。", exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<StoredArchiveDocumentInfo>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var browserModule = await GetModuleAsync(cancellationToken);
        try
        {
            var records = await browserModule.InvokeAsync<BrowserStoredArchiveInfo[]>(
                "listDocuments",
                cancellationToken);
            return records.Select(record => record.ToContract()).ToArray();
        }
        catch (JSException exception)
        {
            throw new IOException("浏览器无法读取本机档案列表。", exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<StoredArchiveDocument?> GetAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var browserModule = await GetModuleAsync(cancellationToken);
        try
        {
            var record = await browserModule.InvokeAsync<BrowserStoredArchiveDocument?>(
                "getDocument",
                cancellationToken,
                documentId.ToString("D"));
            return record is null
                ? null
                : new StoredArchiveDocument
                {
                    Info = record.Info.ToContract(),
                    Content = record.Content
                };
        }
        catch (JSException exception)
        {
            throw new IOException("浏览器无法读取本机档案。", exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<ExternalArchiveDocument?> OpenAsync(
        CancellationToken cancellationToken = default)
    {
        var browserModule = await GetModuleAsync(cancellationToken);
        try
        {
            var document = await browserModule.InvokeAsync<BrowserExternalArchiveDocument?>(
                "openDocument",
                cancellationToken,
                MaximumExternalDocumentBytes);
            return document is null
                ? null
                : new ExternalArchiveDocument
                {
                    FileName = document.FileName,
                    MediaType = document.MediaType,
                    Content = document.Content
                };
        }
        catch (JSException exception)
        {
            throw new IOException("浏览器无法打开外部档案。", exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask SaveAsync(
        ArchiveDocumentExport document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        var browserModule = await GetModuleAsync(cancellationToken);
        try
        {
            await browserModule.InvokeVoidAsync(
                "downloadDocument",
                cancellationToken,
                document.SuggestedFileName,
                document.MediaType,
                document.Content.ToArray());
        }
        catch (JSException exception)
        {
            throw new IOException("浏览器无法导出档案文档。", exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (module is not null)
        {
            try
            {
                await module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }

        moduleLock.Dispose();
    }

    private async ValueTask<IJSObjectReference> GetModuleAsync(CancellationToken cancellationToken)
    {
        if (module is not null)
        {
            return module;
        }

        await moduleLock.WaitAsync(cancellationToken);
        try
        {
            module ??= await jsRuntime.InvokeAsync<IJSObjectReference>(
                "import",
                cancellationToken,
                "./js/archive-storage.js");
            return module;
        }
        finally
        {
            moduleLock.Release();
        }
    }

    private sealed record BrowserStoredArchiveInfo
    {
        public required string DocumentId { get; init; }

        public string? PatientId { get; init; }

        public required string Title { get; init; }

        public required string SubjectDisplay { get; init; }

        public required DateTimeOffset ConsultationStartedAt { get; init; }

        public required DateTimeOffset LastSavedAt { get; init; }

        public required string FormatIdentifier { get; init; }

        public required string FormatVersion { get; init; }

        public required string MediaType { get; init; }

        public StoredArchiveDocumentInfo ToContract() => new()
        {
            DocumentId = Guid.Parse(DocumentId),
            PatientId = Guid.TryParse(PatientId, out var patientId) ? patientId : null,
            Title = Title,
            SubjectDisplay = SubjectDisplay,
            ConsultationStartedAt = ConsultationStartedAt,
            LastSavedAt = LastSavedAt,
            FormatIdentifier = FormatIdentifier,
            FormatVersion = FormatVersion,
            MediaType = MediaType
        };

        public static BrowserStoredArchiveInfo From(StoredArchiveDocumentInfo info) => new()
        {
            DocumentId = info.DocumentId.ToString("D"),
            PatientId = info.PatientId?.ToString("D"),
            Title = info.Title,
            SubjectDisplay = info.SubjectDisplay,
            ConsultationStartedAt = info.ConsultationStartedAt,
            LastSavedAt = info.LastSavedAt,
            FormatIdentifier = info.FormatIdentifier,
            FormatVersion = info.FormatVersion,
            MediaType = info.MediaType
        };
    }

    private sealed record BrowserStoredArchiveDocument
    {
        public required BrowserStoredArchiveInfo Info { get; init; }

        public required byte[] Content { get; init; }
    }

    private sealed record BrowserExternalArchiveDocument
    {
        public string? FileName { get; init; }

        public string? MediaType { get; init; }

        public required byte[] Content { get; init; }
    }
}
