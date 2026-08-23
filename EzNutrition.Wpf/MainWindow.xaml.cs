using EzNutrition.Wpf.Archives;
using EzNutrition.Wpf.Desktop;
using Microsoft.Extensions.Logging;
using System.Windows;

namespace EzNutrition.Wpf;

/// <summary>
/// 承载 Blazor 工作台并提供少量明确的 Windows 原生入口。
/// </summary>
public partial class MainWindow : Window
{
    private readonly ArchiveStorageDirectory archiveStorage;
    private readonly DesktopFileLauncher fileLauncher;
    private readonly ILogger<MainWindow> logger;

    /// <summary>
    /// 创建桌面主窗口。
    /// </summary>
    public MainWindow(
        IServiceProvider services,
        ArchiveStorageDirectory archiveStorage,
        DesktopFileLauncher fileLauncher,
        ILogger<MainWindow> logger)
    {
        InitializeComponent();
        Resources.Add("BlazorServices", services);
        this.archiveStorage = archiveStorage;
        this.fileLauncher = fileLauncher;
        this.logger = logger;
    }

    private void OpenArchiveFolder(object sender, RoutedEventArgs e)
    {
        try
        {
            archiveStorage.EnsureCreated();
            fileLauncher.OpenFolder(archiveStorage.RootPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Unable to open the archive storage directory.");
            MessageBox.Show(
                "无法打开本机档案文件夹，请检查目录权限或宿主配置。",
                "打开失败",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void ExitApplication(object sender, RoutedEventArgs e) => Close();

}
