using EzNutrition.Wpf.Archives;
using EzNutrition.Wpf.Configuration;
using EzNutrition.Wpf.Desktop;
using EzNutrition.Wpf.Security;
using Microsoft.AspNetCore.Components.WebView;
using Microsoft.Extensions.Logging;
using System.Windows;

namespace EzNutrition.Wpf;

/// <summary>
/// 承载 Blazor 工作台并提供少量明确的 Windows 原生入口。
/// </summary>
internal partial class MainWindow : Window
{
    private readonly ArchiveStorageDirectory archiveStorage;
    private readonly DesktopFileLauncher fileLauncher;
    private readonly ILogger<MainWindow> logger;
    private readonly WpfUserDataPaths paths;
    private readonly DpapiLoginCredentialStore credentialStore;
    private readonly WpfHostSettings hostSettings;
    private readonly WpfUserSettingsStore settingsStore;

    /// <summary>
    /// 创建桌面主窗口。
    /// </summary>
    public MainWindow(
        IServiceProvider services,
        ArchiveStorageDirectory archiveStorage,
        DesktopFileLauncher fileLauncher,
        WpfUserDataPaths paths,
        WpfHostSettings hostSettings,
        WpfUserSettingsStore settingsStore,
        DpapiLoginCredentialStore credentialStore,
        ILogger<MainWindow> logger)
    {
        InitializeComponent();
        Resources.Add("BlazorServices", services);
        this.archiveStorage = archiveStorage;
        this.fileLauncher = fileLauncher;
        this.paths = paths;
        this.hostSettings = hostSettings;
        this.settingsStore = settingsStore;
        this.credentialStore = credentialStore;
        this.logger = logger;
        ConfigureConnectionSecurityWarning();
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

    private void OpenServerSettings(object sender, RoutedEventArgs e)
    {
        var window = new ServerSettingsWindow(settingsStore, credentialStore)
        {
            Owner = this
        };
        _ = window.ShowDialog();
    }

    private void ConfigureBlazorWebView(object sender, BlazorWebViewInitializingEventArgs e)
    {
        e.UserDataFolder = paths.WebView2Directory;
        Directory.CreateDirectory(e.UserDataFolder);
    }

    private void ConfigureConnectionSecurityWarning()
    {
        ConnectionSecurityWarningText.Text = hostSettings.TransportSecurity switch
        {
            ServerTransportSecurity.AllowSelfSignedHttps =>
                $"安全警示：当前允许 {hostSettings.ServerBaseAddress.Host} 使用自签名 HTTPS 证书。",
            ServerTransportSecurity.InsecureHttp =>
                $"严重安全警示：当前通过未加密 HTTP 连接 {hostSettings.ServerBaseAddress.Authority}，凭据和业务数据可能被读取或篡改。",
            _ => string.Empty
        };
        ConnectionSecurityWarning.Visibility =
            hostSettings.TransportSecurity == ServerTransportSecurity.StrictHttps
                ? Visibility.Collapsed
                : Visibility.Visible;
    }
}
