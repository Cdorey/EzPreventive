namespace EzNutrition.Wpf.Configuration;

/// <summary>
/// 为小型用户配置与密文文件提供有界读取和同目录原子写入。
/// </summary>
internal static class UserDataFileIO
{
    /// <summary>获取单个用户配置文件允许的最大字节数。</summary>
    internal const int MaximumFileBytes = 64 * 1024;

    /// <summary>完整读取一个大小受限的用户数据文件。</summary>
    internal static byte[] ReadAllBytes(string path)
    {
        using var stream = OpenForRead(path);
        var content = GC.AllocateUninitializedArray<byte>(checked((int)stream.Length));
        stream.ReadExactly(content);
        return content;
    }

    /// <summary>异步完整读取一个大小受限的用户数据文件。</summary>
    internal static async ValueTask<byte[]> ReadAllBytesAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = OpenForRead(path);
        var content = GC.AllocateUninitializedArray<byte>(checked((int)stream.Length));
        await stream.ReadExactlyAsync(content, cancellationToken);
        return content;
    }

    /// <summary>
    /// 先把内容写入目标目录中的临时文件并刷新到磁盘，再替换最终文件。
    /// </summary>
    internal static async ValueTask WriteAtomicallyAsync(
        string destinationPath,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        if (content.IsEmpty || content.Length > MaximumFileBytes)
        {
            throw new InvalidDataException(
                $"用户数据文件大小必须在 1 到 {MaximumFileBytes} 字节之间。");
        }

        var directory = Path.GetDirectoryName(destinationPath)
            ?? throw new InvalidOperationException("用户数据文件没有父目录。");
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
                bufferSize: 16 * 1024,
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

    private static FileStream OpenForRead(string path)
    {
        var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 16 * 1024,
            FileOptions.SequentialScan);
        if (stream.Length <= 0 || stream.Length > MaximumFileBytes)
        {
            stream.Dispose();
            throw new InvalidDataException(
                $"用户数据文件大小必须在 1 到 {MaximumFileBytes} 字节之间。");
        }

        return stream;
    }
}
