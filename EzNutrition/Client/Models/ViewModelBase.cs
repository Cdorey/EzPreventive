using AntDesign;
using EzNutrition.Client.Services;
using Microsoft.AspNetCore.Components;

namespace EzNutrition.Client.Models
{
    public abstract class ViewModelBase
    {
        protected readonly HttpClient _httpClient;
        protected readonly UserSessionService _userSession;
        protected readonly NavigationManager _navigationManager;
        protected readonly IMessageService _message;

        protected ViewModelBase(IMessageService message, HttpClient httpClient, UserSessionService userSession, NavigationManager navigationManager)
        {
            _httpClient = httpClient;
            _navigationManager = navigationManager;
            _userSession = userSession;
            _message = message;
        }
    }
}
