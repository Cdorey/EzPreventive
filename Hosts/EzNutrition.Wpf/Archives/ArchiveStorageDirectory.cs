namespace EzNutrition.Wpf.Archives;

/// <summary>
/// 表示桌面宿主管理的档案根目录。
/// </summary>
public sealed class ArchiveStorageDirectory
{
    private static readonly string[] DefaultSegments = ["EzSuit", "EzNutrition", "Archives"];

    private ArchiveStorageDirectory(string rootPath)
    {
        RootPath = rootPath;
    }

    /// <summary>获取规范化后的绝对档案根路径。</summary>
    public string RootPath { get; }

    /// <summary>
    /// 根据可选配置创建档案目录描述；未配置时使用用户本地应用数据目录。
    /// </summary>
    public static ArchiveStorageDirectory Create(string? configuredPath)
    {
        var candidate = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Path.Combine(DefaultSegments))
            : Environment.ExpandEnvironmentVariables(configuredPath.Trim());

        if (string.IsNullOrWhiteSpace(candidate) || !Path.IsPathFullyQualified(candidate))
        {
            throw new InvalidOperationException("本机档案目录必须是绝对文件系统路径。");
        }

        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
        var pathRoot = Path.TrimEndingDirectorySeparator(Path.GetPathRoot(fullPath) ?? string.Empty);
        if (string.Equals(fullPath, pathRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("不能把磁盘根目录用作本机档案目录。");
        }

        return new ArchiveStorageDirectory(fullPath);
    }

    /// <summary>确保档案根目录存在。</summary>
    public void EnsureCreated() => Directory.CreateDirectory(RootPath);
}
