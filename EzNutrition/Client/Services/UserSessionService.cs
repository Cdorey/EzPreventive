﻿using EzNutrition.Client.Models;
using EzNutrition.Shared.Data.Entities;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using System.ComponentModel;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Security.Claims;

namespace EzNutrition.Client.Services
{
    public class UserSessionService(IHttpClientFactory httpClientFactory) : AuthenticationStateProvider, IAccessTokenProvider, INotifyPropertyChanged
    {
        private readonly HttpClient _client = httpClientFactory.CreateClient("Anonymous");
        private UserInfo? userInfo;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event Action? OnStateChanged;

        public UserInfo? UserInfo
        {
            get => userInfo;
            private set
            {
                if (userInfo != value)
                {
                    userInfo = value;
                    OnPropertyChanged();
                    NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
                    OnStateChanged?.Invoke();
                }
            }
        }

        private string caseNumber = string.Empty;
        public string CaseNumber
        {
            get => caseNumber;
            private set { caseNumber = value; OnPropertyChanged(); }
        }

        private string coverLetter = string.Empty;
        public string CoverLetter
        {
            get => coverLetter;
            private set { coverLetter = value; OnPropertyChanged(); }
        }

        private string notice = string.Empty;
        public string Notice
        {
            get => notice;
            private set { notice = value; OnPropertyChanged(); }
        }

        public bool IsTouchDetected { get; set; } = false;

        public async Task GetSystemInfoAsync()
        {
            CaseNumber = await _client.GetStringAsync("SystemInfo/CaseNumber/");

            try
            {
                Notice? coverLetter = await _client.GetFromJsonAsync<Notice>("SystemInfo/CoverLetter/");
                CoverLetter = coverLetter?.Description ?? string.Empty;
            }
            catch (HttpRequestException) { CoverLetter = string.Empty; }

            try
            {
                Notice? notice = await _client.GetFromJsonAsync<Notice>("SystemInfo/Notice/");
                Notice = notice?.Description ?? string.Empty;
            }
            catch (HttpRequestException) { Notice = string.Empty; }
            OnStateChanged?.Invoke();
        }

        public async ValueTask<AccessTokenResult> RequestAccessToken()
        {
            if (UserInfo == null || string.IsNullOrEmpty(UserInfo.Token))
            {
                return new AccessTokenResult(AccessTokenResultStatus.RequiresRedirect,
                                             new AccessToken(),
                                             "/Index",
                                             new InteractiveRequestOptions { Interaction = InteractionType.SignIn, ReturnUrl = "/Index" });
            }



            DateTimeOffset expiresAt = UserInfo.ExpiresAt ?? DateTimeOffset.UtcNow.AddMinutes(30); // 默认30分钟

            var token = new AccessToken
            {
                Value = UserInfo.Token,
                Expires = expiresAt
            };

            return new AccessTokenResult(AccessTokenResultStatus.Success,
                                         token,
                                         "/Index",
                                         new InteractiveRequestOptions { Interaction = InteractionType.SignIn, ReturnUrl = "/Index" });
        }

        public async ValueTask<AccessTokenResult> RequestAccessToken(AccessTokenRequestOptions options)
        {
            return await RequestAccessToken();
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            ClaimsPrincipal userPrincipal = UserInfo != null
                ? new ClaimsPrincipal(new ClaimsIdentity(UserInfo.Claims, "jwt"))
                : new ClaimsPrincipal(new ClaimsIdentity());
            return new AuthenticationState(userPrincipal);
        }

        public async Task SignInAsync(string userName, string password)
        {
            if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password)) return;

            var formContent = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>(nameof(userName), userName),
                new KeyValuePair<string, string>(nameof(password), password)
            ]);

            HttpResponseMessage res = await _client.PostAsync("Auth/Login", formContent);
            if (!res.IsSuccessStatusCode)
            {
                throw new Exception(await res.Content.ReadAsStringAsync());
            }

            var token = await res.Content.ReadAsStringAsync();
            UserInfo = new UserInfo(token);
        }

        public async Task SignOutAsync()
        {
            UserInfo = null;
        }

        public async Task RegistAsync(string? userName, string? password)
        {
            if (!(string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password)))
            {
                var formContent = new FormUrlEncodedContent([new KeyValuePair<string, string>(nameof(userName), userName), new KeyValuePair<string, string>(nameof(password), password)]);
                HttpResponseMessage res = await _client.PostAsync("Auth/Regist/Epiman", formContent);
                if (res.IsSuccessStatusCode)
                {
                    await SignInAsync(userName, password);
                }
                else
                {
                    throw new Exception(await res.Content.ReadAsStringAsync());
                }
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
