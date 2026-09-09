using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
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
        ArchiveDocumentStoreCapabilities.Clear |
        ArchiveDocumentStoreCapabilities.CompareExchange;

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
            using var writer = AcquireWriter();
            await SaveCoreAsync(document, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> CompareExchangeAsync(
        StoredArchiveDocument document,
        ReadOnlyMemory<byte>? expectedContent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ValidateInfo(document.Info);
        if (document.Content.IsEmpty || document.Content.Length > ArchiveFileIO.MaximumDocumentBytes)
            throw new InvalidDataException("档案正文为空或超出桌面宿主允许的大小。");

        await gate.WaitAsync(cancellationToken);
        try
        {
            EnsureDirectories();
            using var writer = AcquireWriter();
            var catalogPath = GetCatalogPath(document.Info.DocumentId);
            if (File.Exists(catalogPath))
            {
                if (expectedContent is null) return false;
                var current = await ReadCatalogEntryAsync(catalogPath, cancellationToken);
                var bytes = await ArchiveFileIO.ReadAllBytesAsync(GetContentPath(current), cancellationToken);
                if (!bytes.AsSpan().SequenceEqual(expectedContent.Value.Span)) return false;
            }
            else if (expectedContent is not null)
            {
                return false;
            }

            await SaveCoreAsync(document, cancellationToken);
            return true;
        }
        finally { gate.Release(); }
    }

    /// <summary>通过正文指纹分离文件版本，以索引替换作为唯一提交点。</summary>
    private async ValueTask SaveCoreAsync(StoredArchiveDocument document, CancellationToken cancellationToken)
    {
        var extension = GetSafeExtension(document.Info.PreferredFileExtension);
        var fingerprint = Convert.ToHexString(SHA256.HashData(document.Content.Span));
        var contentFileName = $"{document.Info.DocumentId:N}.{fingerprint}{extension}";
        var contentPath = Path.Combine(storage.RootPath, contentFileName);
        var catalog = new CatalogEntry
        {
            Version = CatalogVersion,
            Info = document.Info,
            ContentFileName = contentFileName
        };

        await ArchiveFileIO.WriteAtomicallyAsync(contentPath, document.Content, cancellationToken);
        await ArchiveFileIO.WriteAtomicallyAsync(
            GetCatalogPath(document.Info.DocumentId),
            JsonSerializer.SerializeToUtf8Bytes(catalog, JsonOptions), cancellationToken);
        // 索引已经提交成功。清理失败不应把一次成功保存伪装成失败，旧文件可稍后清理。
        try { DeleteSupersededContentFiles(document.Info.DocumentId, contentFileName); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { logger.LogWarning(exception, "Archive was committed but obsolete content files could not be removed."); }
    }

    /// <summary>串行化同一档案目录的跨进程写入；占用时明确失败，交由用户重试。</summary>
    private FileStream AcquireWriter() => new(
        Path.Combine(CatalogPath, ".write.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

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
            EnsureDirectories();
            using var writer = AcquireWriter();
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
            using var writer = AcquireWriter();
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
            !HasDocumentFileName(fileName, entry.Info.DocumentId))
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
            if (HasDocumentFileName(Path.GetFileName(path), documentId)
                && !string.Equals(Path.GetFileName(path), currentFileName, StringComparison.OrdinalIgnoreCase))
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
                Guid.TryParseExact(Path.GetFileNameWithoutExtension(fileName).Split('.')[0], "N", out var id) &&
                HasDocumentFileName(fileName, id))
            {
                File.Delete(path);
            }
        }
    }

    /// <summary>兼容旧版直接文件名和新版带正文指纹的文件名，不接纳任意路径。</summary>
    private static bool HasDocumentFileName(string fileName, Guid id)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var prefix = id.ToString("N");
        return string.Equals(stem, prefix, StringComparison.OrdinalIgnoreCase)
            || (stem.Length == 97 && stem.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase)
                && stem.AsSpan(33).IndexOfAnyExcept("0123456789abcdefABCDEF") < 0);
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
