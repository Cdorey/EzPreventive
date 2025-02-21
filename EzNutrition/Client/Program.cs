using EzNutrition.Client.Models;
using EzNutrition.Client.Services;
using EzNutrition.Shared.Policies;
using Microsoft.AspNetCore.Components.Authorization;
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

            builder.Services.AddAuthorizationCore(PolicyList.RegisterPolicies);

            builder.Services.AddHttpClient("Anonymous", client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress));
            builder.Services.AddHttpClient("Authorize", client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)).AddHttpMessageHandler<CustomAuthorizationMessageHandler>();
            builder.Services.AddScoped<CustomAuthorizationMessageHandler>();
            //builder.Services.AddScoped<AuthenticationStateProvider, RemoteAuthenticationService<RemoteAuthenticationState,UserInfo,ServiceProviderOptions>>();
            builder.Services.AddScoped<MainTreatmentViewModel>();
            //builder.Services.AddScoped<AuthenticationStateProvider, UserSessionService>();
            builder.Services.AddSingleton<UserSessionService>();
            builder.Services.AddScoped<AuthenticationStateProvider>(provider => provider.GetRequiredService<UserSessionService>());
            builder.Services.AddScoped<ArchiveManageService>();
            builder.Services.AddAntDesign();
            builder.Services.AddOptions();
            await builder.Build().RunAsync();
        }
    }
}