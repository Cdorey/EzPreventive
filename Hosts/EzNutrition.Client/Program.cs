using EzNutrition.Application.Archives;
using EzNutrition.Assessments.Common;
using EzNutrition.Archives.Contracts.Validation;
using EzNutrition.Archives.Contracts.ValueObjects;
using EzNutrition.Archives.Contracts.Serialization;
using EzNutrition.Archives.Xml;
using EzNutrition.Client.Infrastructure;
using EzNutrition.Domain.Assessments;
using EzNutrition.Presentation;
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
            builder.RootComponents.Add<EzNutrition.Presentation.App>("#app");
            builder.RootComponents.Add<HeadOutlet>("head::after");

            builder.Services.AddEzNutritionPresentation(
                new Uri(builder.HostEnvironment.BaseAddress),
                TimeZoneInfo.Local);
            builder.Services.AddSingleton<INutritionAssessmentInstrument, Nrs2002Instrument>();
            builder.Services.AddSingleton(CreateArchiveContractAssembler());
            builder.Services.AddSingleton<IArchiveValidator, ArchiveContractValidator>();
            builder.Services.AddSingleton<IArchiveCodec, XmlArchiveCodec>();
            builder.Services.AddScoped<BrowserArchiveGateway>();
            builder.Services.AddScoped<IArchiveDocumentStore>(provider =>
                provider.GetRequiredService<BrowserArchiveGateway>());
            builder.Services.AddScoped<IArchiveDocumentTransport>(provider =>
                provider.GetRequiredService<BrowserArchiveGateway>());
            builder.Services.AddScoped<IArchiveWorkflow, ArchiveWorkflow>();
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
