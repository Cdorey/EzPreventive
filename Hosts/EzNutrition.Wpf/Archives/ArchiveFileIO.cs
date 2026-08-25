namespace EzNutrition.Wpf.Archives;

/// <summary>
/// 提供档案文件的统一大小限制与同目录原子写入。
/// </summary>
internal static class ArchiveFileIO
{
    /// <summary>获取单份档案允许的最大字节数。</summary>
    internal const int MaximumDocumentBytes = 16 * 1024 * 1024;

    /// <summary>
    /// 在读取前核对文件大小，并完整读取一份档案。
    /// </summary>
    internal static async ValueTask<byte[]> ReadAllBytesAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var length = stream.Length;
        if (length <= 0 || length > MaximumDocumentBytes)
        {
            throw new InvalidDataException(
                $"档案文档大小必须在 1 到 {MaximumDocumentBytes} 字节之间。");
        }

        var content = GC.AllocateUninitializedArray<byte>((int)length);
        await stream.ReadExactlyAsync(content, cancellationToken);
        return content;
    }

    /// <summary>
    /// 先在目标目录写入临时文件，再替换最终文件，避免暴露半写入内容。
    /// </summary>
    internal static async ValueTask WriteAtomicallyAsync(
        string destinationPath,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        if (content.IsEmpty || content.Length > MaximumDocumentBytes)
        {
            throw new InvalidDataException(
                $"档案文档大小必须在 1 到 {MaximumDocumentBytes} 字节之间。");
        }

        var directory = Path.GetDirectoryName(destinationPath)
            ?? throw new InvalidOperationException("档案目标路径没有父目录。");
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(content, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, destinationPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
