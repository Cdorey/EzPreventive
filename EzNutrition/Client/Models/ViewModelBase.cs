using AntDesign;
using EzNutrition.Client.Services;
using Microsoft.AspNetCore.Components;

namespace EzNutrition.Client.Models
{
    public abstract class ViewModelBase(IMessageService message, IHttpClientFactory httpClientFactory, UserSessionService userSession, NavigationManager navigationManager)
    {
        protected readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        protected readonly UserSessionService _userSession = userSession;
        protected readonly NavigationManager _navigationManager = navigationManager;
        protected readonly IMessageService _message = message;
    }
}
