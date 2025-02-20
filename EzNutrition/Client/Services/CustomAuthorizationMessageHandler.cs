using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace EzNutrition.Client.Services
{
    public class CustomAuthorizationMessageHandler(UserSessionService provider, NavigationManager navigation) : BaseAddressAuthorizationMessageHandler(provider, navigation)
    {
    }
}
