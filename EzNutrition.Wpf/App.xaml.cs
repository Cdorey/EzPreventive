using System.Reflection;
using EzNutrition.Application.Archives;
using EzNutrition.Archives.Contracts.Serialization;
using EzNutrition.Archives.Contracts.Validation;
using EzNutrition.Archives.Contracts.ValueObjects;
using EzNutrition.Archives.Xml;
using EzNutrition.Presentation;
using EzNutrition.Wpf.Archives;
using EzNutrition.Wpf.Configuration;
using EzNutrition.Wpf.Desktop;
using EzNutrition.Wpf.Networking;
using EzNutrition.Wpf.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Windows;

namespace EzNutrition.Wpf;

/// <summary>
/// 管理 WPF Hybrid 宿主的进程生命周期与依赖组合。
/// </summary>
public partial class App : System.Windows.Application
{
    private IHost? host;

    /// <summary>
    /// 创建桌面应用，并安装进程级异常兜底。
    /// </summary>
    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;
    }

    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            host = CreateHost(e.Args);
            host.StartAsync().GetAwaiter().GetResult();
            MainWindow = host.Services.GetRequiredService<MainWindow>();
            MainWindow.Show();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"EzNutrition 桌面宿主无法启动。\n\n{exception.Message}",
                "启动失败",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            if (host is not null)
            {
                try
                {
                    host.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
                }
                finally
                {
                    host.Dispose();
                }
            }
        }
        finally
        {
            AppDomain.CurrentDomain.UnhandledException -= HandleUnhandledException;
            base.OnExit(e);
        }
    }

    /// <summary>
    /// 建立仅属于 WPF 宿主的组合根。
    /// </summary>
    private static IHost CreateHost(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ApplicationName = typeof(App).Assembly.GetName().Name,
            ContentRootPath = AppContext.BaseDirectory
        });
        builder.Logging.AddDebug();
#if DEBUG
        builder.Logging.AddFilter("Microsoft.AspNetCore.Components.WebView", LogLevel.Trace);
#endif

        var userDataPaths = WpfUserDataPaths.CreateDefault();
        var userOverrides = WpfUserSettingsStore.ReadConfigurationOverrides(
            userDataPaths.SettingsFilePath,
            out var userSettingsWarning);
        if (userOverrides.Count > 0)
        {
            builder.Configuration.AddInMemoryCollection(userOverrides);
        }

        // 用户设置覆盖随应用发布的默认值；环境变量与命令行仍保留最高优先级，
        // 便于机构通过受控部署系统临时纠正端点。
        builder.Configuration.AddEnvironmentVariables();
        if (args.Length > 0)
        {
            builder.Configuration.AddCommandLine(args);
        }

        var settings = WpfHostSettings.Create(builder.Configuration);
        var userSettingsStore = new WpfUserSettingsStore(userDataPaths, settings);
        var credentialStore = new DpapiLoginCredentialStore(userDataPaths, settings);
        var httpMessageHandlerFactory = new WpfHttpMessageHandlerFactory(settings);
        var services = builder.Services;
        services.AddSingleton(userDataPaths);
        services.AddSingleton(settings);
        services.AddSingleton(userSettingsStore);
        services.AddSingleton(credentialStore);
        services.AddSingleton(settings.ArchiveStorage);

        services.AddWpfBlazorWebView();
#if DEBUG
        services.AddBlazorWebViewDeveloperTools();
#endif
        services.AddEzNutritionPresentation(
            settings.ServerBaseAddress,
            TimeZoneInfo.Local,
            credentialStore,
            httpMessageHandlerFactory.Create);

        services.AddSingleton(CreateArchiveContractAssembler());
        services.AddSingleton<IArchiveValidator, ArchiveContractValidator>();
        services.AddSingleton<IArchiveCodec, XmlArchiveCodec>();
        services.AddSingleton<FileSystemArchiveDocumentStore>();
        services.AddSingleton<IArchiveDocumentStore>(provider =>
            provider.GetRequiredService<FileSystemArchiveDocumentStore>());
        services.AddSingleton<WpfArchiveDocumentTransport>();
        services.AddSingleton<IArchiveDocumentTransport>(provider =>
            provider.GetRequiredService<WpfArchiveDocumentTransport>());
        services.AddScoped<IArchiveWorkflow, ArchiveWorkflow>();

        services.AddSingleton<DesktopFileLauncher>();
        services.AddSingleton<MainWindow>();
        var builtHost = builder.Build();
        if (!string.IsNullOrWhiteSpace(userSettingsWarning))
        {
            builtHost.Services
                .GetRequiredService<ILogger<App>>()
                .LogWarning("{UserSettingsWarning}", userSettingsWarning);
        }

        return builtHost;
    }

    /// <summary>
    /// 创建带桌面宿主来源标识的档案组装器。
    /// </summary>
    private static ArchiveContractAssembler CreateArchiveContractAssembler()
    {
        var assembly = typeof(App).Assembly;
        var version = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (string.IsNullOrWhiteSpace(version))
        {
            version = assembly.GetName().Version?.ToString() ?? "unknown";
        }

        return new ArchiveContractAssembler(new EzNutrition.Archives.Contracts.ValueObjects.ApplicationIdentity(
            new Uri("https://eznutrition.cdorey.net/applications/wpf-hybrid"),
            "EzNutrition WPF Hybrid",
            version));
    }

    private void HandleUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        host?.Services
            .GetService<ILogger<App>>()?
            .LogCritical(exception, "The WPF Hybrid host terminated because of an unhandled exception.");

#if DEBUG
        var message = e.ExceptionObject.ToString() ?? "发生未知的未处理异常。";
#else
        const string message = "EzNutrition 遇到无法恢复的错误，即将退出。请重新启动应用；若问题持续出现，请联系维护人员。";
#endif
        MessageBox.Show(
            message,
            "EzNutrition 运行错误",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
