using EzNutrition.Presentation.Services;
using EzNutrition.Wpf.Configuration;
using Microsoft.Extensions.Logging;

namespace EzNutrition.Wpf.Desktop;

/// <summary>
/// 使用非模态 WPF 窗口承载共享辅助页面。
/// </summary>
internal sealed class WpfAuxiliaryPageHost(
    IServiceProvider services,
    WpfUserDataPaths paths,
    ILoggerFactory loggerFactory) : IAuxiliaryPageHost
{
    /// <inheritdoc />
    public bool CanOpenInNativeWindow => true;

    /// <inheritdoc />
    public async ValueTask OpenInNativeWindowAsync(AuxiliaryPage page)
    {
        var application = System.Windows.Application.Current ??
            throw new InvalidOperationException("WPF 应用尚未启动。");
        var dispatcher = application.Dispatcher;
        if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            throw new InvalidOperationException("WPF 应用正在关闭，无法再打开辅助窗口。");
        }

        if (dispatcher.CheckAccess())
        {
            OpenWindow(application, page);
            return;
        }

        await dispatcher.InvokeAsync(() => OpenWindow(application, page));
    }

    private void OpenWindow(System.Windows.Application application, AuxiliaryPage page)
    {
        var window = new AuxiliaryPageWindow(
            services,
            paths,
            page,
            loggerFactory.CreateLogger<AuxiliaryPageWindow>());
        if (application.MainWindow?.IsVisible == true)
        {
            window.Owner = application.MainWindow;
        }

        window.Show();
    }
}
