namespace EzNutrition.Wpf.Tests;

/// <summary>
/// 为文件系统测试提供范围明确、可核验的临时目录。
/// </summary>
internal sealed class TempDirectory : IDisposable
{
    private static readonly string TestRoot = Path.GetFullPath(
        Path.Combine(Path.GetTempPath(), "EzNutrition.Wpf.Tests"));

    /// <summary>
    /// 创建独立的测试目录。
    /// </summary>
    public TempDirectory()
    {
        Directory.CreateDirectory(TestRoot);
        RootPath = Path.Combine(TestRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
    }

    /// <summary>获取本次测试独占的绝对目录。</summary>
    public string RootPath { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        var fullPath = Path.GetFullPath(RootPath);
        var requiredPrefix = Path.TrimEndingDirectorySeparator(TestRoot) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("拒绝清理测试根目录之外的路径。");
        }

        if (Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, recursive: true);
        }
    }
}
