namespace EzNutrition.Server.Services
{
    public sealed class CertificateFileStore(
        IWebHostEnvironment environment,
        ILogger<CertificateFileStore> logger)
    {
        public const long MaxFileSize = 50L * 1024 * 1024;

        private static readonly IReadOnlyDictionary<string, string> ContentTypes =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [".jpg"] = "image/jpeg",
                [".jpeg"] = "image/jpeg",
                [".png"] = "image/png"
            };

        private readonly string rootPath = Path.Combine(environment.ContentRootPath, "TempUploads");

        public async Task SaveAsync(Guid ticket, IFormFile file, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(file);

            if (file.Length <= 0 || file.Length > MaxFileSize)
            {
                throw new InvalidDataException("The certificate image is empty or exceeds the size limit.");
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!ContentTypes.ContainsKey(extension))
            {
                throw new InvalidDataException("Only JPEG and PNG certificate images are supported.");
            }

            Directory.CreateDirectory(rootPath);
            var finalPath = GetPath(ticket, extension);
            var temporaryPath = Path.Combine(rootPath, $".{ticket:D}.{Guid.NewGuid():N}.tmp");

            try
            {
                await using (var input = file.OpenReadStream())
                await using (var output = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    var header = new byte[8];
                    var bytesRead = await input.ReadAtLeastAsync(
                        header,
                        header.Length,
                        throwOnEndOfStream: false,
                        cancellationToken);

                    if (!HasExpectedSignature(extension, header.AsSpan(0, bytesRead)))
                    {
                        throw new InvalidDataException("The uploaded file content does not match its image extension.");
                    }

                    await output.WriteAsync(header.AsMemory(0, bytesRead), cancellationToken);
                    await input.CopyToAsync(output, cancellationToken);
                    await output.FlushAsync(cancellationToken);
                }

                File.Move(temporaryPath, finalPath, overwrite: true);
                DeleteAlternativeExtensions(ticket, finalPath);
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    logger.LogWarning(ex, "Failed to remove temporary certificate file {TemporaryPath}.", temporaryPath);
                }
            }
        }

        public CertificateFile? OpenRead(Guid ticket)
        {
            foreach (var pair in ContentTypes)
            {
                var path = GetPath(ticket, pair.Key);
                if (!File.Exists(path))
                {
                    continue;
                }

                var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                return new CertificateFile(stream, pair.Value);
            }

            return null;
        }

        public void Delete(Guid ticket)
        {
            foreach (var extension in ContentTypes.Keys)
            {
                var path = GetPath(ticket, extension);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        internal IReadOnlyList<StoredCertificateFile> EnumerateStoredFiles(
            CancellationToken cancellationToken = default)
        {
            if (!Directory.Exists(rootPath))
            {
                return [];
            }

            try
            {
                var files = new List<StoredCertificateFile>();
                foreach (var path in Directory.EnumerateFiles(rootPath, "*", SearchOption.TopDirectoryOnly))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var fileInfo = new FileInfo(path);
                    fileInfo.Refresh();
                    if (!fileInfo.Exists ||
                        !TryParseStoredFile(fileInfo.Name, out var ticket, out var kind))
                    {
                        continue;
                    }

                    files.Add(new StoredCertificateFile(
                        fileInfo.Name,
                        ticket,
                        new DateTimeOffset(fileInfo.LastWriteTimeUtc),
                        kind));
                }

                return files;
            }
            catch (DirectoryNotFoundException)
            {
                return [];
            }
        }

        internal bool Delete(StoredCertificateFile file)
        {
            ArgumentNullException.ThrowIfNull(file);
            if (!string.Equals(Path.GetFileName(file.FileName), file.FileName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("A stored certificate file name cannot contain a directory path.");
            }

            var path = Path.Combine(rootPath, file.FileName);
            if (!File.Exists(path))
            {
                return false;
            }

            File.Delete(path);
            return true;
        }

        private string GetPath(Guid ticket, string extension) =>
            Path.Combine(rootPath, $"{ticket:D}{extension}");

        private void DeleteAlternativeExtensions(Guid ticket, string retainedPath)
        {
            foreach (var extension in ContentTypes.Keys)
            {
                var path = GetPath(ticket, extension);
                if (!string.Equals(path, retainedPath, StringComparison.OrdinalIgnoreCase) && File.Exists(path))
                {
                    try
                    {
                        File.Delete(path);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        logger.LogWarning(ex, "Failed to remove superseded certificate file {Path}.", path);
                    }
                }
            }
        }

        private static bool TryParseStoredFile(
            string fileName,
            out Guid ticket,
            out StoredCertificateFileKind kind)
        {
            var extension = Path.GetExtension(fileName);
            if (ContentTypes.ContainsKey(extension) &&
                Guid.TryParseExact(Path.GetFileNameWithoutExtension(fileName), "D", out ticket))
            {
                kind = StoredCertificateFileKind.Final;
                return true;
            }

            if (string.Equals(extension, ".tmp", StringComparison.OrdinalIgnoreCase))
            {
                var segments = fileName.Split('.', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length == 3 &&
                    Guid.TryParseExact(segments[0], "D", out ticket) &&
                    Guid.TryParseExact(segments[1], "N", out var nonce) &&
                    string.Equals(
                        fileName,
                        $".{ticket:D}.{nonce:N}.tmp",
                        StringComparison.OrdinalIgnoreCase))
                {
                    kind = StoredCertificateFileKind.Temporary;
                    return true;
                }
            }

            ticket = default;
            kind = default;
            return false;
        }

        private static bool HasExpectedSignature(string extension, ReadOnlySpan<byte> header) =>
            extension switch
            {
                ".jpg" or ".jpeg" =>
                    header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
                ".png" =>
                    header.Length >= 8 && header[..8].SequenceEqual(
                        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
                _ => false
            };
    }

    public sealed record CertificateFile(Stream Content, string ContentType);

    internal enum StoredCertificateFileKind
    {
        Final,
        Temporary
    }

    internal sealed record StoredCertificateFile(
        string FileName,
        Guid Ticket,
        DateTimeOffset LastModifiedUtc,
        StoredCertificateFileKind Kind);
}
