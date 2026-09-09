using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

/// <summary>
/// 手动运行的桌面报告适配验收工具。使用隔离 WebView2 目录和合成 PDF，
/// 捕获真实预览与打印对话框；不会提交实际打印任务，不加入常规无界面单元测试。
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("用法：WpfProbe <合成报告.pdf> <验收输出目录>");
            return 2;
        }

        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var exitCode = 1;
        application.Startup += async (_, _) =>
        {
            Window? window = null;
            try
            {
                var pdf = await File.ReadAllBytesAsync(Path.GetFullPath(args[0]));
                var output = Path.GetFullPath(args[1]);
                Directory.CreateDirectory(output);
                // 仅验收工具通过反射进入现有内部窗口，不为测试扩大产品 API。
                var host = Assembly.Load("EzNutrition.Wpf");
                var type = host.GetType("EzNutrition.Wpf.Reports.WpfReportPrinter+ReportWindow", throwOnError: true)!;
                window = (Window)Activator.CreateInstance(type, "合成量表报告验收")!;
                window.ShowActivated = false;
                window.ShowInTaskbar = false;
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Left = -2000;
                window.Top = 0;
                var panel = (DockPanel)window.Content;
                var viewer = panel.Children.OfType<WebView2>().Single();
                var print = panel.Children.OfType<Button>().Single();
                var navigation = new TaskCompletionSource<bool>();
                var served = new TaskCompletionSource<byte[]>();
                viewer.CoreWebView2InitializationCompleted += (_, initialized) =>
                {
                    if (!initialized.IsSuccess)
                    {
                        navigation.TrySetException(initialized.InitializationException);
                        return;
                    }
                    viewer.CoreWebView2.NavigationCompleted += (_, completed) =>
                        navigation.TrySetResult(completed.IsSuccess);
                };
                window.Show();
                var load = (Task)type.GetMethod("LoadAsync")!.Invoke(window,
                    [new ReadOnlyMemory<byte>(pdf), Path.Combine(output, "WebView2"), CancellationToken.None])!;
                await load.WaitAsync(TimeSpan.FromSeconds(30));
                if (!await navigation.Task.WaitAsync(TimeSpan.FromSeconds(20)) || !print.IsEnabled)
                    throw new InvalidOperationException("PDF 导航失败或打印入口未启用。");
                // 内置 PDF 查看器会把 ResponseReceived 的正文换成 embed 容器 HTML。
                // 在宿主已设置响应后观察实际提供的流；COM 包装流不可定位，读完后以相同字节补回。
                viewer.CoreWebView2.WebResourceRequested += (_, response) =>
                {
                    if (!response.Request.Uri.StartsWith("https://eznutrition-report.invalid/", StringComparison.Ordinal)) return;
                    try
                    {
                        var content = response.Response.Content;
                        using var memory = new MemoryStream();
                        content.CopyTo(memory);
                        var bytes = memory.ToArray();
                        response.Response.Content = new MemoryStream(bytes, writable: false);
                        served.TrySetResult(bytes);
                    }
                    catch (Exception error) { served.TrySetException(error); }
                };
                viewer.CoreWebView2.Reload();
                var actual = await served.Task.WaitAsync(TimeSpan.FromSeconds(20));
                if (!pdf.AsSpan().SequenceEqual(actual))
                    throw new InvalidDataException("查看器收到的内容与输入原件不同。");
                // PDF 内置查看器在导航完成后异步绘制，留出短暂渲染时间再做视觉验收。
                await Task.Delay(1500);
                await Capture(viewer, Path.Combine(output, "preview.png"));
                print.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Task.Delay(1500);
                await Capture(viewer, Path.Combine(output, "after-print-command.png"));
                var windows = AutomationElement.RootElement.FindAll(TreeScope.Children,
                    new OrCondition(
                        new PropertyCondition(AutomationElement.ProcessIdProperty, Environment.ProcessId),
                        new PropertyCondition(AutomationElement.ProcessIdProperty, (int)viewer.CoreWebView2.BrowserProcessId)));
                var controls = new List<string>();
                var hasPrintDialog = false;
                foreach (AutomationElement owned in windows)
                {
                    foreach (AutomationElement control in owned.FindAll(TreeScope.Subtree, System.Windows.Automation.Condition.TrueCondition))
                    {
                        hasPrintDialog |= control.Current.ControlType == ControlType.Window
                            && control.Current.Name is "打印" or "Print";
                        controls.Add($"{control.Current.ControlType.ProgrammaticName}: {control.Current.Name}");
                    }
                }
                await File.WriteAllLinesAsync(Path.Combine(output, "print-controls.txt"), controls);
                if (!hasPrintDialog) throw new InvalidOperationException("未找到桌面打印对话框。");
                await File.WriteAllTextAsync(Path.Combine(output, "verification.txt"),
                    $"PDF navigation: success\nOriginal SHA-256: {Convert.ToHexString(SHA256.HashData(actual))}\nPrint dialog detected; no print job submitted.\n");
                Console.WriteLine("WPF 原件响应一致，实际打印对话框已出现；预览截图仍需视觉检查。");
                exitCode = 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); }
            finally
            {
                window?.Close();
                application.Shutdown();
            }
        };
        application.Run();
        return exitCode;
    }

    private static async Task Capture(WebView2 viewer, string path)
    {
        await using var image = File.Create(path);
        await viewer.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, image);
    }
}
