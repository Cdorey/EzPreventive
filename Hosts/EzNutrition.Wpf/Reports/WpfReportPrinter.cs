using EzNutrition.Application.Reports;
using EzNutrition.Wpf.Configuration;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Windows;
using System.Windows.Controls;

namespace EzNutrition.Wpf.Reports;

/// <summary>使用独立 WebView2 显示内存中的 PDF 原件并提供打印入口，不生成临时患者文件。</summary>
internal sealed class WpfReportPrinter(WpfUserDataPaths paths) : IReportPrinter
{
    /// <inheritdoc />
    public async ValueTask PrintAsync(ReadOnlyMemory<byte> pdf, string title, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            var window = new ReportWindow(title);
            window.Owner = System.Windows.Application.Current.MainWindow;
            window.Show();
            try { await window.LoadAsync(pdf, paths.WebView2Directory, cancellationToken); }
            catch { window.Close(); throw; }
        }).Task.Unwrap();
    }

    private sealed class ReportWindow : Window
    {
        private readonly WebView2 viewer = new();
        private readonly Button print = new() { Content = "打印", IsEnabled = false, Padding = new Thickness(18, 7, 18, 7), Margin = new Thickness(8) };
        private byte[] pdf = [];
        private bool closed;
        private readonly string documentUri = $"https://eznutrition-report.invalid/{Guid.NewGuid():N}.pdf";

        public ReportWindow(string title)
        {
            Title = title + " - 打印预览";
            Width = 1000;
            Height = 800;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var panel = new DockPanel();
            DockPanel.SetDock(print, Dock.Top);
            panel.Children.Add(print);
            panel.Children.Add(viewer);
            Content = panel;
            print.Click += (_, _) => viewer.CoreWebView2.ShowPrintUI(CoreWebView2PrintDialogKind.Browser);
            Closed += (_, _) => { closed = true; viewer.Dispose(); pdf = []; };
        }

        public async Task LoadAsync(ReadOnlyMemory<byte> content, string userDataDirectory, CancellationToken token)
        {
            pdf = content.ToArray();
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataDirectory);
            token.ThrowIfCancellationRequested();
            if (closed) return;
            await viewer.EnsureCoreWebView2Async(environment);
            if (closed) return;
            var core = viewer.CoreWebView2;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            core.WebResourceRequested += (_, args) =>
            {
                // 虚拟地址仅用来向内置查看器提供 PDF；不会解析域名或访问网络。
                args.Response = args.Request.Uri == documentUri
                    ? environment.CreateWebResourceResponse(new MemoryStream(pdf, writable: false), 200, "OK",
                        "Content-Type: application/pdf\r\nCache-Control: no-store")
                    : environment.CreateWebResourceResponse(null, 403, "Forbidden", "");
            };
            core.NavigationStarting += (_, args) => args.Cancel = args.Uri != documentUri;
            core.NewWindowRequested += (_, args) => args.Handled = true;
            core.NavigationCompleted += (_, args) => print.IsEnabled = args.IsSuccess;
            core.Navigate(documentUri);
        }
    }
}
