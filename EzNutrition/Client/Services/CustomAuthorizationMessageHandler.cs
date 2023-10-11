using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components;

namespace EzNutrition.Client.Services
{
    public class CustomAuthorizationMessageHandler : BaseAddressAuthorizationMessageHandler
    {
        public CustomAuthorizationMessageHandler(UserSessionService provider, NavigationManager navigation) : base(provider, navigation)
        {

        }
    }
}
