using System.Text.Json;
using System.Text.Json.Serialization;
using EzNutrition.Application.Archives;
using Microsoft.Extensions.Logging;

namespace EzNutrition.Wpf.Archives;

/// <summary>
/// 将编码档案保存为可独立取用的文档文件，并在隐藏目录维护格式无关索引。
/// </summary>
public sealed class FileSystemArchiveDocumentStore : IArchiveDocumentStore, IDisposable
{
    private const int CatalogVersion = 1;
    private const string CatalogDirectoryName = ".catalog";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private readonly ArchiveStorageDirectory storage;
    private readonly ILogger<FileSystemArchiveDocumentStore> logger;
    private readonly SemaphoreSlim gate = new(1, 1);

    /// <summary>
    /// 创建文件系统档案存储。
    /// </summary>
    public FileSystemArchiveDocumentStore(
        ArchiveStorageDirectory storage,
        ILogger<FileSystemArchiveDocumentStore> logger)
    {
        this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public ArchiveDocumentStoreCapabilities Capabilities =>
        ArchiveDocumentStoreCapabilities.Save |
        ArchiveDocumentStoreCapabilities.Browse |
        ArchiveDocumentStoreCapabilities.Delete |
        ArchiveDocumentStoreCapabilities.Clear;

    /// <inheritdoc />
    public async ValueTask SaveAsync(
        StoredArchiveDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ValidateInfo(document.Info);
        if (document.Content.IsEmpty || document.Content.Length > ArchiveFileIO.MaximumDocumentBytes)
        {
            throw new InvalidDataException("档案正文为空或超出桌面宿主允许的大小。");
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            EnsureDirectories();
            var extension = GetSafeExtension(document.Info.PreferredFileExtension);
            var contentFileName = $"{document.Info.DocumentId:N}{extension}";
            var contentPath = Path.Combine(storage.RootPath, contentFileName);
            var catalogPath = GetCatalogPath(document.Info.DocumentId);
            var catalog = new CatalogEntry
            {
                Version = CatalogVersion,
                Info = document.Info,
                ContentFileName = contentFileName
            };

            await ArchiveFileIO.WriteAtomicallyAsync(contentPath, document.Content, cancellationToken);
            await ArchiveFileIO.WriteAtomicallyAsync(
                catalogPath,
                JsonSerializer.SerializeToUtf8Bytes(catalog, JsonOptions),
                cancellationToken);
            DeleteSupersededContentFiles(document.Info.DocumentId, contentFileName);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<StoredArchiveDocumentInfo>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            EnsureDirectories();
            var records = new List<StoredArchiveDocumentInfo>();
            foreach (var catalogPath in Directory.EnumerateFiles(CatalogPath, "*.json", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var entry = await ReadCatalogEntryAsync(catalogPath, cancellationToken);
                    if (File.Exists(GetContentPath(entry)))
                    {
                        records.Add(entry.Info);
                    }
                    else
                    {
                        logger.LogWarning(
                            "Archive catalog {CatalogPath} points to a missing content file.",
                            catalogPath);
                    }
                }
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
                {
                    logger.LogWarning(exception, "Ignoring an unreadable archive catalog entry {CatalogPath}.", catalogPath);
                }
            }

            return records
                .OrderByDescending(record => record.LastSavedAt)
                .ThenBy(record => record.DocumentId)
                .ToArray();
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<StoredArchiveDocument?> GetAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        if (documentId == Guid.Empty)
        {
            return null;
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            EnsureDirectories();
            var catalogPath = GetCatalogPath(documentId);
            if (!File.Exists(catalogPath))
            {
                return null;
            }

            var entry = await ReadCatalogEntryAsync(catalogPath, cancellationToken);
            var contentPath = GetContentPath(entry);
            if (!File.Exists(contentPath))
            {
                throw new InvalidDataException("档案索引对应的文档文件不存在。");
            }

            return new StoredArchiveDocument
            {
                Info = entry.Info,
                Content = await ArchiveFileIO.ReadAllBytesAsync(contentPath, cancellationToken)
            };
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DeleteAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        if (documentId == Guid.Empty)
        {
            return;
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            var catalogPath = GetCatalogPath(documentId);
            if (!File.Exists(catalogPath))
            {
                return;
            }

            var entry = await ReadCatalogEntryAsync(catalogPath, cancellationToken);
            File.Delete(GetContentPath(entry));
            File.Delete(catalogPath);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            EnsureDirectories();
            foreach (var catalogPath in Directory.EnumerateFiles(CatalogPath, "*.json", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var entry = await ReadCatalogEntryAsync(catalogPath, cancellationToken);
                    File.Delete(GetContentPath(entry));
                }
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
                {
                    logger.LogWarning(
                        exception,
                        "Removing an unreadable catalog entry without guessing its content path: {CatalogPath}.",
                        catalogPath);
                }

                File.Delete(catalogPath);
            }

            DeleteOrphanedContentFiles();
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => gate.Dispose();

    private string CatalogPath => Path.Combine(storage.RootPath, CatalogDirectoryName);

    private string GetCatalogPath(Guid documentId) =>
        Path.Combine(CatalogPath, $"{documentId:N}.json");

    private string GetContentPath(CatalogEntry entry)
    {
        var fileName = Path.GetFileName(entry.ContentFileName);
        if (!string.Equals(fileName, entry.ContentFileName, StringComparison.Ordinal) ||
            !string.Equals(
                Path.GetFileNameWithoutExtension(fileName),
                entry.Info.DocumentId.ToString("N"),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("档案索引包含不安全的文档文件名。");
        }

        return Path.Combine(storage.RootPath, fileName);
    }

    private void EnsureDirectories()
    {
        storage.EnsureCreated();
        Directory.CreateDirectory(CatalogPath);
        try
        {
            File.SetAttributes(CatalogPath, File.GetAttributes(CatalogPath) | FileAttributes.Hidden);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogDebug(exception, "Unable to mark the archive catalog directory as hidden.");
        }
    }

    private async ValueTask<CatalogEntry> ReadCatalogEntryAsync(
        string catalogPath,
        CancellationToken cancellationToken)
    {
        var bytes = await ArchiveFileIO.ReadAllBytesAsync(catalogPath, cancellationToken);
        var entry = JsonSerializer.Deserialize<CatalogEntry>(bytes, JsonOptions)
            ?? throw new InvalidDataException("档案索引为空。");
        if (entry.Version != CatalogVersion)
        {
            throw new InvalidDataException($"不支持档案索引版本 {entry.Version}。");
        }

        ValidateInfo(entry.Info);
        var expectedCatalogName = $"{entry.Info.DocumentId:N}.json";
        if (!string.Equals(Path.GetFileName(catalogPath), expectedCatalogName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("档案索引文件名与文档标识不一致。");
        }

        _ = GetContentPath(entry);
        return entry;
    }

    private void DeleteSupersededContentFiles(Guid documentId, string currentFileName)
    {
        foreach (var path in Directory.EnumerateFiles(
                     storage.RootPath,
                     $"{documentId:N}.*",
                     SearchOption.TopDirectoryOnly))
        {
            if (!string.Equals(Path.GetFileName(path), currentFileName, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(path);
            }
        }
    }

    private void DeleteOrphanedContentFiles()
    {
        foreach (var path in Directory.EnumerateFiles(storage.RootPath, "*", SearchOption.TopDirectoryOnly))
        {
            var fileName = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(Path.GetExtension(fileName)) &&
                Guid.TryParseExact(Path.GetFileNameWithoutExtension(fileName), "N", out _))
            {
                File.Delete(path);
            }
        }
    }

    private static string GetSafeExtension(string? preferredExtension)
    {
        var extension = string.IsNullOrWhiteSpace(preferredExtension) ? ".archive" : preferredExtension;
        if (extension.Length is < 2 or > 16 ||
            extension[0] != '.' ||
            extension.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            extension.IndexOfAny(['/', '\\']) >= 0)
        {
            throw new InvalidDataException("档案格式声明了不安全的文件扩展名。");
        }

        return extension.ToLowerInvariant();
    }

    private static void ValidateInfo(StoredArchiveDocumentInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        if (info.DocumentId == Guid.Empty ||
            string.IsNullOrWhiteSpace(info.Title) ||
            string.IsNullOrWhiteSpace(info.SubjectDisplay) ||
            string.IsNullOrWhiteSpace(info.FormatIdentifier) ||
            string.IsNullOrWhiteSpace(info.FormatVersion) ||
            string.IsNullOrWhiteSpace(info.MediaType) ||
            !Uri.TryCreate(info.FormatIdentifier, UriKind.Absolute, out _))
        {
            throw new InvalidDataException("档案索引元数据不完整或无效。");
        }
    }

    private sealed record CatalogEntry
    {
        public required int Version { get; init; }

        public required StoredArchiveDocumentInfo Info { get; init; }

        public required string ContentFileName { get; init; }
    }
}
