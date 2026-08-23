using System.Reflection;
using AntDesign;
using EzNutrition.Application.Archives;
using EzNutrition.Application.Consultations;
using EzNutrition.Application.Ports;
using EzNutrition.Archives.Contracts.Serialization;
using EzNutrition.Archives.Contracts.Validation;
using EzNutrition.Archives.Contracts.ValueObjects;
using EzNutrition.Archives.Xml;
using EzNutrition.Client.Infrastructure;
using EzNutrition.Client.Services;
using EzNutrition.Shared.Policies;
using EzNutrition.UI.Services;
using EzNutrition.Wpf.Archives;
using EzNutrition.Wpf.Configuration;
using EzNutrition.Wpf.Desktop;
using Microsoft.AspNetCore.Components.Authorization;
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

        var settings = WpfHostSettings.Create(builder.Configuration);
        var services = builder.Services;
        services.AddSingleton(settings);
        services.AddSingleton(settings.ArchiveStorage);
        services.AddSingleton(new ApplicationServerEndpoint(settings.ServerBaseAddress));

        services.AddWpfBlazorWebView();
#if DEBUG
        services.AddBlazorWebViewDeveloperTools();
#endif
        services.AddAuthorizationCore(PolicyList.RegisterPolicies);
        services.AddOptions();
        services.AddAntDesign();

        services.AddHttpClient("Anonymous", client => client.BaseAddress = settings.ServerBaseAddress);
        services
            .AddHttpClient("Authorize", client => client.BaseAddress = settings.ServerBaseAddress)
            .AddHttpMessageHandler<CustomAuthorizationMessageHandler>();
        services
            .AddHttpClient("AiAuthorize", client =>
            {
                client.BaseAddress = settings.ServerBaseAddress;
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .AddHttpMessageHandler<CustomAuthorizationMessageHandler>();
        services.AddTransient<CustomAuthorizationMessageHandler>();

        services.AddSingleton<UserSessionService>();
        services.AddScoped<AuthenticationStateProvider>(provider =>
            provider.GetRequiredService<UserSessionService>());
        services.AddScoped<HttpNutritionDataSource>();
        services.AddScoped<IAiAdviceGateway, HttpAiAdviceGateway>();
        services.AddScoped<IEnergyReferenceDataSource>(provider =>
            provider.GetRequiredService<HttpNutritionDataSource>());
        services.AddScoped<IDietaryReferenceIntakeDataSource>(provider =>
            provider.GetRequiredService<HttpNutritionDataSource>());
        services.AddScoped<IFoodCompositionDataSource>(provider =>
            new SessionCachedFoodCatalogDataSource(
                provider.GetRequiredService<HttpNutritionDataSource>()));
        services.AddScoped<AiAdviceApplicationService>();
        services.AddScoped<ConsultationApplicationService>();
        services.AddScoped<ConsultationWorkspaceManager>();
        services.AddScoped<AccountService>();
        services.AddScoped<CertificateUploadService>();

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
        services.AddSingleton<ILocalDateTimeFormatter>(
            new LocalDateTimeFormatter(TimeZoneInfo.Local));

        services.AddSingleton<DesktopFileLauncher>();
        services.AddSingleton<MainWindow>();
        return builder.Build();
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
