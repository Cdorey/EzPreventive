using EzNutrition.Presentation.Services;
using EzNutrition.Wpf.Configuration;
using Microsoft.AspNetCore.Components.WebView;
using Microsoft.Extensions.Logging;
using System.Windows;

namespace EzNutrition.Wpf;

/// <summary>
/// 在独立的 WPF WebView 中承载一个共享辅助页面。
/// </summary>
internal partial class AuxiliaryPageWindow : Window
{
    private readonly ILogger<AuxiliaryPageWindow> logger;
    private readonly WpfUserDataPaths paths;

    /// <summary>
    /// 创建辅助页面窗口，并把 WebView 的初始路由设置为指定页面。
    /// </summary>
    internal AuxiliaryPageWindow(
        IServiceProvider services,
        WpfUserDataPaths paths,
        AuxiliaryPage page,
        ILogger<AuxiliaryPageWindow> logger)
    {
        ArgumentNullException.ThrowIfNull(services);
        this.paths = paths ?? throw new ArgumentNullException(nameof(paths));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

        InitializeComponent();
        Title = $"{page.GetTitle()} · EzNutrition";
        BlazorView.StartPath = $"/{page.GetRelativePath()}";
        Resources.Add("BlazorServices", services);
    }

    /// <inheritdoc />
    protected override async void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        try
        {
            await BlazorView.DisposeAsync();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Unable to dispose the auxiliary Blazor WebView.");
        }
    }

    private void ConfigureBlazorWebView(
        object sender,
        BlazorWebViewInitializingEventArgs e)
    {
        e.UserDataFolder = paths.WebView2Directory;
        Directory.CreateDirectory(e.UserDataFolder);
    }
}
