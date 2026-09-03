using AntDesign;
using EzNutrition.Application.Consultations;
using EzNutrition.Application.Ports;
using EzNutrition.Presentation.Infrastructure;
using EzNutrition.Presentation.Services;
using EzNutrition.Shared.Policies;
using EzNutrition.UI.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace EzNutrition.Presentation;

/// <summary>
/// 提供交互式客户端宿主共享的工作台服务注册。
/// </summary>
public static class PresentationServiceCollectionExtensions
{
    /// <summary>
    /// 注册 Razor 工作台、远程服务适配器与当前进程内的用户会话。
    /// </summary>
    /// <param name="services">目标服务集合。</param>
    /// <param name="serverBaseAddress">EzNutrition 服务端的绝对 HTTP(S) 基地址。</param>
    /// <param name="displayTimeZone">向用户显示档案时间时采用的时区。</param>
    /// <param name="primaryHttpMessageHandlerFactory">
    /// 宿主可选提供的主 HTTP 处理器工厂；每次调用必须返回新的处理器实例。
    /// </param>
    /// <returns>传入的服务集合，便于继续配置具体宿主。</returns>
    /// <remarks>
    /// 本方法不注册宿主认证客户端、档案存储、文件交互或文档来源标识；这些能力具有平台语义，
    /// 必须由 WASM、WPF 或后续宿主在各自组合根中提供。
    /// </remarks>
    public static IServiceCollection AddEzNutritionPresentation(
        this IServiceCollection services,
        Uri serverBaseAddress,
        TimeZoneInfo displayTimeZone,
        Func<HttpMessageHandler>? primaryHttpMessageHandlerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(serverBaseAddress);
        ArgumentNullException.ThrowIfNull(displayTimeZone);

        var endpoint = new ApplicationServerEndpoint(serverBaseAddress);
        services.AddSingleton(endpoint);
        services.AddAuthorizationCore(PolicyList.RegisterPolicies);
        services.AddOptions();
        services.AddAntDesign();

        var anonymousClient = services.AddHttpClient(
            "Anonymous",
            client => client.BaseAddress = endpoint.BaseAddress);
        var authenticationClient = services.AddHttpClient("Authentication", client =>
        {
            client.BaseAddress = endpoint.BaseAddress;
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        var authorizedClient = services
            .AddHttpClient("Authorize", client => client.BaseAddress = endpoint.BaseAddress)
            .AddHttpMessageHandler<CustomAuthorizationMessageHandler>();
        var aiClient = services
            .AddHttpClient("AiAuthorize", client =>
            {
                client.BaseAddress = endpoint.BaseAddress;
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .AddHttpMessageHandler<CustomAuthorizationMessageHandler>();
        if (primaryHttpMessageHandlerFactory is not null)
        {
            anonymousClient.ConfigurePrimaryHttpMessageHandler(primaryHttpMessageHandlerFactory);
            authenticationClient.ConfigurePrimaryHttpMessageHandler(primaryHttpMessageHandlerFactory);
            authorizedClient.ConfigurePrimaryHttpMessageHandler(primaryHttpMessageHandlerFactory);
            aiClient.ConfigurePrimaryHttpMessageHandler(primaryHttpMessageHandlerFactory);
        }

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
        services.AddScoped<NutritionAssessmentApplicationService>();
        services.AddScoped<ConsultationApplicationService>();
        services.AddScoped<ConsultationWorkspaceManager>();
        services.AddScoped<AccountService>();
        services.AddScoped<CertificateUploadService>();
        services.AddSingleton<ILocalDateTimeFormatter>(
            new LocalDateTimeFormatter(displayTimeZone));

        return services;
    }
}
