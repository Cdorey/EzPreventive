namespace EzNutrition.Wpf.Configuration;

/// <summary>
/// 集中描述由 WPF 宿主管理、且属于当前 Windows 用户的应用数据路径。
/// </summary>
internal sealed class WpfUserDataPaths
{
    private WpfUserDataPaths(string rootPath)
    {
        RootPath = rootPath;
        SettingsFilePath = Path.Combine(rootPath, "settings.json");
        CredentialsDirectory = Path.Combine(rootPath, "Credentials");
        WebView2Directory = Path.Combine(rootPath, "WebView2");
    }

    /// <summary>获取 WPF 宿主的用户数据根目录。</summary>
    internal string RootPath { get; }

    /// <summary>获取用户级连接设置文件路径。</summary>
    internal string SettingsFilePath { get; }

    /// <summary>获取 Windows 保护登录信息的目录。</summary>
    internal string CredentialsDirectory { get; }

    /// <summary>获取 WebView2 用户数据目录。</summary>
    internal string WebView2Directory { get; }

    /// <summary>使用当前用户的 LocalApplicationData 创建默认路径集合。</summary>
    internal static WpfUserDataPaths CreateDefault()
    {
        var localApplicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        return Create(Path.Combine(localApplicationData, "EzSuit", "EzNutrition"));
    }

    /// <summary>从指定根目录创建路径集合，供启动组合与文件系统测试使用。</summary>
    internal static WpfUserDataPaths Create(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Path.IsPathFullyQualified(rootPath))
        {
            throw new InvalidOperationException("WPF 用户数据根目录必须是绝对路径。");
        }

        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        var pathRoot = Path.TrimEndingDirectorySeparator(
            Path.GetPathRoot(fullPath) ?? string.Empty);
        if (string.Equals(fullPath, pathRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("不能把磁盘根目录用作 WPF 用户数据目录。");
        }

        return new WpfUserDataPaths(fullPath);
    }
}
