namespace EzNutrition.Server.Services
{
    /// <summary>
    /// 管理专业认证证件图片的保存、读取、删除及受控目录枚举。
    /// </summary>
    /// <param name="environment">用于确定应用内容根目录的 Web 宿主环境。</param>
    /// <param name="logger">日志记录器。</param>
    public sealed class CertificateFileStore(
        IWebHostEnvironment environment,
        ILogger<CertificateFileStore> logger)
    {
        /// <summary>
        /// 单个证件图片允许的最大字节数。
        /// </summary>
        public const long MaxFileSize = 50L * 1024 * 1024;

        private static readonly IReadOnlyDictionary<string, string> ContentTypes =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [".jpg"] = "image/jpeg",
                [".jpeg"] = "image/jpeg",
                [".png"] = "image/png"
            };

        private readonly string rootPath = Path.Combine(environment.ContentRootPath, "TempUploads");

        /// <summary>
        /// 校验并以原子替换方式保存指定 Ticket 的证件图片。
        /// </summary>
        /// <param name="ticket">专业认证申请对应的上传 Ticket。</param>
        /// <param name="file">待保存的 JPEG 或 PNG 文件。</param>
        /// <param name="cancellationToken">用于取消文件读取和写入的令牌。</param>
        /// <returns>表示异步保存操作的任务。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="file"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="InvalidDataException">文件为空、超过大小限制、扩展名不受支持或内容签名不匹配。</exception>
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

        /// <summary>
        /// 打开指定 Ticket 对应的证件图片以供只读访问。
        /// </summary>
        /// <param name="ticket">专业认证申请对应的上传 Ticket。</param>
        /// <returns>包含文件流和内容类型的证件文件；不存在时返回 <see langword="null"/>。</returns>
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

        /// <summary>
        /// 删除指定 Ticket 对应的全部受支持图片格式。
        /// </summary>
        /// <param name="ticket">专业认证申请对应的上传 Ticket。</param>
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

        /// <summary>
        /// 枚举临时上传目录中符合本组件最终文件或上传临时文件命名规则的文件。
        /// </summary>
        /// <remarks>
        /// 无法识别的文件名会被忽略；目录尚未创建或在枚举期间被移除时返回空集合。
        /// </remarks>
        /// <param name="cancellationToken">用于取消目录枚举的令牌。</param>
        /// <returns>当前可识别文件的快照。</returns>
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

        /// <summary>
        /// 精确删除由 <see cref="EnumerateStoredFiles"/> 返回的单个存储文件。
        /// </summary>
        /// <param name="file">待删除的受控存储文件描述。</param>
        /// <returns>文件存在并成功删除时为 <see langword="true"/>；文件已不存在时为 <see langword="false"/>。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="file"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="InvalidOperationException">文件描述中包含目录路径。</exception>
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

    /// <summary>
    /// 表示已打开的证件文件及其响应内容类型。
    /// </summary>
    /// <param name="Content">由调用方负责释放的只读文件流。</param>
    /// <param name="ContentType">文件对应的 HTTP 内容类型。</param>
    public sealed record CertificateFile(Stream Content, string ContentType);

    /// <summary>
    /// 表示证件存储目录中可识别文件的类型。
    /// </summary>
    internal enum StoredCertificateFileKind
    {
        /// <summary>
        /// 已完成写入、以 Ticket 命名的证件图片。
        /// </summary>
        Final,

        /// <summary>
        /// 原子写入过程中使用、以点号开头的临时文件。
        /// </summary>
        Temporary
    }

    /// <summary>
    /// 描述证件存储目录中的一个受控文件。
    /// </summary>
    /// <param name="FileName">不含目录部分的文件名。</param>
    /// <param name="Ticket">从文件名解析出的专业认证 Ticket。</param>
    /// <param name="LastModifiedUtc">文件最后修改时间，采用 UTC。</param>
    /// <param name="Kind">文件类型。</param>
    internal sealed record StoredCertificateFile(
        string FileName,
        Guid Ticket,
        DateTimeOffset LastModifiedUtc,
        StoredCertificateFileKind Kind);
}
