using EzNutrition.Wpf.Archives;
using EzNutrition.Wpf.Configuration;
using EzNutrition.Wpf.Desktop;
using EzNutrition.Wpf.Security;
using EzNutrition.Wpf.Updates;
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
    private readonly VelopackUpdateService updateService;
    private readonly CancellationTokenSource updateCheckCancellation = new();
    private bool updateCheckStarted;

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
        VelopackUpdateService updateService,
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
        this.updateService = updateService;
        this.logger = logger;
        ConfigureConnectionSecurityWarning();
    }

    /// <inheritdoc />
    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        if (updateCheckStarted)
        {
            return;
        }

        updateCheckStarted = true;
        _ = CheckForApplicationUpdateAsync(updateCheckCancellation.Token);
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        updateCheckCancellation.Cancel();
        updateCheckCancellation.Dispose();
        base.OnClosed(e);
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

    private async Task CheckForApplicationUpdateAsync(CancellationToken cancellationToken)
    {
        try
        {
            var update = await updateService.CheckAndPrepareAsync(cancellationToken);
            if (update is null || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            logger.LogInformation(
                "Prepared EzNutrition desktop update from {CurrentVersion} to {TargetVersion}.",
                update.CurrentVersion,
                update.TargetVersion);
            if (!update.ChangesCompatibilityLine)
            {
                return;
            }

            var answer = MessageBox.Show(
                this,
                $"EzNutrition {update.TargetVersion} 已经准备就绪（当前版本 {update.CurrentVersion}）。\n\n" +
                "此更新改变了产品或 HTTP 接口契约的兼容代际。继续使用当前版本时，部分接口请求可能出现异常。\n\n" +
                "建议确认当前咨询内容已经保存或归档，然后立即重启完成更新。选择“否”可以暂时继续，更新将在下次启动时应用。\n\n" +
                "是否立即重启？",
                "建议更新 EzNutrition",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);
            if (answer != MessageBoxResult.Yes)
            {
                return;
            }

            ScheduleUpdateAndRestart();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 主窗口关闭时停止后台更新准备，不向用户显示无意义的失败提示。
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Unable to check or download a desktop update.");
        }
    }

    private void ScheduleUpdateAndRestart()
    {
        try
        {
            updateService.SchedulePreparedUpdateForRestart();
            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to start the prepared desktop update.");
            MessageBox.Show(
                this,
                "无法启动已经下载的更新。请完成并保存当前工作后，关闭再重新打开 EzNutrition；更新将在下次启动时重试。",
                "无法启动更新",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
