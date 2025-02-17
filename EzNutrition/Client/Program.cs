using EzNutrition.Client;
using EzNutrition.Client.Models;
using EzNutrition.Client.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using System.Net.Http;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EzNutrition.Client
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");
            builder.RootComponents.Add<HeadOutlet>("head::after");

            builder.Services.AddAuthorizationCore();
            builder.Services.AddHttpClient("Anonymous", client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress));
            builder.Services.AddHttpClient("Authorize", client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)).AddHttpMessageHandler<CustomAuthorizationMessageHandler>();
            builder.Services.AddScoped<CustomAuthorizationMessageHandler>();
            //builder.Services.AddSingleton<AuthenticationStateProvider, RemoteAuthenticationService<RemoteAuthenticationState,UserInfo,ServiceProviderOptions>>();

            builder.Services.AddScoped<MainTreatmentViewModel>();
            builder.Services.AddSingleton<AuthenticationStateProvider, UserSessionService>();
            builder.Services.AddSingleton<UserSessionService>();
            builder.Services.AddSingleton<ArchiveManageService>();

            builder.Services.AddAntDesign();
            builder.Services.AddOptions();
            await builder.Build().RunAsync();
        }
    }
}