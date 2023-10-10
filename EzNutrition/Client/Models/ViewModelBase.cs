using AntDesign;
using EzNutrition.Client.Services;
using Microsoft.AspNetCore.Components;

namespace EzNutrition.Client.Models
{
    public abstract class ViewModelBase
    {
        protected readonly IHttpClientFactory _httpClientFactory;
        protected readonly UserSessionService _userSession;
        protected readonly NavigationManager _navigationManager;
        protected readonly IMessageService _message;

        protected ViewModelBase(IMessageService message, IHttpClientFactory httpClientFactory, UserSessionService userSession, NavigationManager navigationManager)
        {
            _navigationManager = navigationManager;
            _httpClientFactory = httpClientFactory;
            _userSession = userSession;
            _message = message;
        }
    }
}
