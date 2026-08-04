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
}
