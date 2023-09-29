using EzNutrition.Client;
using EzNutrition.Client.Models;
using EzNutrition.Client.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace EzNutrition.Client
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");
            builder.RootComponents.Add<HeadOutlet>("head::after");

            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
            builder.Services.AddScoped<MainTreatmentViewModel>();
            builder.Services.AddSingleton<UserSessionService>();
            builder.Services.AddAntDesign();

            await builder.Build().RunAsync();
        }
    }
}