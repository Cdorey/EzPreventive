using AntDesign;
using EzNutrition.Client.Services;
using EzNutrition.Shared.Data.Entities;
using EzNutrition.Shared.Utilities;
using Microsoft.AspNetCore.Components;
using System.Net.Http;
using System;
using System.Text;
using System.Net.Http.Json;

namespace EzNutrition.Client.Models
{
    public class MainTreatmentViewModel(IMessageService message, IHttpClientFactory httpClientFactory, UserSessionService userSession, NavigationManager navigationManager) : ViewModelBase(message, httpClientFactory, userSession, navigationManager)
    {
        //Authorized client
        //private readonly HttpClient _httpClient = httpClientFactory.CreateClient("Authorize");
        //private BreakpointType[] types = new[] { BreakpointType.Xxl, BreakpointType.Xl, BreakpointType.Lg, BreakpointType.Md, BreakpointType.Sm, BreakpointType.Xs };

        public BreakpointType CurrentBreakpoint { get; set; }
    }
}
