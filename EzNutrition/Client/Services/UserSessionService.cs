using System.IdentityModel.Tokens.Jwt;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using System.Net.Http;
using System.Net.Http.Json;
using EzNutrition.Client.Models;
using EzNutrition.Shared.Data.Entities;

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

        public async Task GetSystemInfoAsync()
        {
            CaseNumber = await _client.GetStringAsync("SystemInfo/CaseNumber/");

            try
            {
                var coverLetter = await _client.GetFromJsonAsync<Notice>("SystemInfo/CoverLetter/");
                CoverLetter = coverLetter?.Description ?? string.Empty;
            }
            catch (HttpRequestException) { CoverLetter = string.Empty; }

            try
            {
                var notice = await _client.GetFromJsonAsync<Notice>("SystemInfo/Notice/");
                Notice = notice?.Description ?? string.Empty;
            }
            catch (HttpRequestException) { Notice = string.Empty; }
        }

        public async ValueTask<AccessTokenResult> RequestAccessToken()
        {
            if (UserInfo == null || string.IsNullOrEmpty(UserInfo.Token))
            {
                return new AccessTokenResult(AccessTokenResultStatus.RequiresRedirect, new AccessToken(), "/Index");
            }

            var expiresAt = UserInfo.ExpiresAt ?? DateTimeOffset.UtcNow.AddMinutes(30); // 默认30分钟

            var token = new AccessToken
            {
                Value = UserInfo.Token,
                Expires = expiresAt
            };

            return new AccessTokenResult(AccessTokenResultStatus.Success, token, "/Index");
        }

        public async ValueTask<AccessTokenResult> RequestAccessToken(AccessTokenRequestOptions options)
        {
            return await RequestAccessToken();
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var userPrincipal = UserInfo != null
                ? new ClaimsPrincipal(new ClaimsIdentity(UserInfo.ParseToken(), "jwt"))
                : new ClaimsPrincipal(new ClaimsIdentity());

            return new AuthenticationState(userPrincipal);
        }

        public async Task SignInAsync(string userName, string password)
        {
            if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password)) return;

            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>(nameof(userName), userName),
                new KeyValuePair<string, string>(nameof(password), password)
            });

            var res = await _client.PostAsync("Auth", formContent);
            if (!res.IsSuccessStatusCode)
            {
                throw new Exception(await res.Content.ReadAsStringAsync());
            }

            var token = await res.Content.ReadAsStringAsync();
            var jwtHandler = new JwtSecurityTokenHandler();
            var jwtToken = jwtHandler.ReadJwtToken(token);

            var expClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "exp")?.Value;
            var expiresAt = expClaim != null
                ? DateTimeOffset.FromUnixTimeSeconds(long.Parse(expClaim))
                : DateTimeOffset.UtcNow.AddMinutes(30);

            UserInfo = new UserInfo(token) { ExpiresAt = expiresAt };
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
                var res = await _client.PostAsync("Auth/Regist/Epiman", formContent);
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
