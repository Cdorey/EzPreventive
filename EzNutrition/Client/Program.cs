using EzNutrition.Application.Archives;
using EzNutrition.Application.Consultations;
using EzNutrition.Application.Ports;
using EzNutrition.Archives.Contracts.Validation;
using EzNutrition.Archives.Contracts.ValueObjects;
using EzNutrition.Client.Infrastructure;
using EzNutrition.Client.Services;
using EzNutrition.Shared.Policies;
using EzNutrition.UI.Http;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using System.Reflection;

namespace EzNutrition.Client
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");
            builder.RootComponents.Add<HeadOutlet>("head::after");

            builder.Services.AddAuthorizationCore(PolicyList.RegisterPolicies);

            builder.Services.AddHttpClient("Anonymous", client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress));
            builder.Services
                .AddHttpClient("Authorize", client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
                .AddHttpMessageHandler<CustomAuthorizationMessageHandler>();
            builder.Services
                .AddHttpClient("AiAuthorize", client =>
                {
                    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
                    // AI generation has its own explicit timeout and user cancellation source.
                    client.Timeout = Timeout.InfiniteTimeSpan;
                })
                .AddHttpMessageHandler<CustomAuthorizationMessageHandler>();
            builder.Services.AddTransient<CustomAuthorizationMessageHandler>();
            //builder.Services.AddScoped<AuthenticationStateProvider, RemoteAuthenticationService<RemoteAuthenticationState,UserInfo,ServiceProviderOptions>>();
            //builder.Services.AddScoped<AuthenticationStateProvider, UserSessionService>();
            builder.Services.AddSingleton<UserSessionService>();
            builder.Services.AddScoped<AuthenticationStateProvider>(provider => provider.GetRequiredService<UserSessionService>());
            builder.Services.AddScoped<HttpNutritionDataSource>();
            builder.Services.AddSingleton<IStreamingHttpRequestConfigurator, BrowserStreamingHttpRequestConfigurator>();
            builder.Services.AddScoped<IEnergyReferenceDataSource>(provider => provider.GetRequiredService<HttpNutritionDataSource>());
            builder.Services.AddScoped<IDietaryReferenceIntakeDataSource>(provider => provider.GetRequiredService<HttpNutritionDataSource>());
            builder.Services.AddScoped<IFoodCompositionDataSource>(provider => provider.GetRequiredService<HttpNutritionDataSource>());
            builder.Services.AddScoped<ConsultationApplicationService>();
            builder.Services.AddScoped<ConsultationWorkspaceManager>();
            builder.Services.AddSingleton(CreateArchiveContractAssembler());
            builder.Services.AddSingleton<IArchiveValidator, ArchiveContractValidator>();
            builder.Services.AddScoped<CertificateUploadService>();
            builder.Services.AddAntDesign();
            builder.Services.AddOptions();
            await builder.Build().RunAsync();
        }

        private static ArchiveContractAssembler CreateArchiveContractAssembler()
        {
            var assembly = typeof(Program).Assembly;
            var version = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;
            if (string.IsNullOrWhiteSpace(version))
            {
                version = assembly.GetName().Version?.ToString() ?? "unknown";
            }

            return new ArchiveContractAssembler(new ApplicationIdentity(
                new Uri("https://eznutrition.cdorey.net/applications/wasm-client"),
                "EzNutrition WASM",
                version));
        }
    }
}
