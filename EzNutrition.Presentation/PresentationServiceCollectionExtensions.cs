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
    /// <returns>传入的服务集合，便于继续配置具体宿主。</returns>
    /// <remarks>
    /// 本方法不注册档案存储、文件交互或文档来源标识；这些能力具有平台语义，
    /// 必须由 WASM、WPF 或后续宿主在各自组合根中提供。
    /// </remarks>
    public static IServiceCollection AddEzNutritionPresentation(
        this IServiceCollection services,
        Uri serverBaseAddress,
        TimeZoneInfo displayTimeZone)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(serverBaseAddress);
        ArgumentNullException.ThrowIfNull(displayTimeZone);

        var endpoint = new ApplicationServerEndpoint(serverBaseAddress);
        services.AddSingleton(endpoint);
        services.AddAuthorizationCore(PolicyList.RegisterPolicies);
        services.AddOptions();
        services.AddAntDesign();

        services.AddHttpClient("Anonymous", client => client.BaseAddress = endpoint.BaseAddress);
        services
            .AddHttpClient("Authorize", client => client.BaseAddress = endpoint.BaseAddress)
            .AddHttpMessageHandler<CustomAuthorizationMessageHandler>();
        services
            .AddHttpClient("AiAuthorize", client =>
            {
                client.BaseAddress = endpoint.BaseAddress;
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
        services.AddSingleton<ILocalDateTimeFormatter>(
            new LocalDateTimeFormatter(displayTimeZone));

        return services;
    }
}
