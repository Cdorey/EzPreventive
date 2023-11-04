using EzNutrition.Client.Models;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using System.Net.Http;
using System.Security.Claims;

namespace EzNutrition.Client.Services
{
    public class UserSessionService : AuthenticationStateProvider, IAccessTokenProvider
    {
        private UserInfo? userInfo;
        private readonly HttpClient _client;

        public UserInfo? UserInfo
        {
            get => userInfo;
            private set
            {
                userInfo = value;
                NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
            }
        }

        public string CaseNumber { get; private set; }

        public async Task GetSystemInfoAsync()
        {
            CaseNumber = await _client.GetStringAsync("SystemInfo/CaseNumber/");
        }

        public async ValueTask<AccessTokenResult> RequestAccessToken()
        {
            AccessTokenResult res;
            if (userInfo == default)
            {
                res = new AccessTokenResult(AccessTokenResultStatus.RequiresRedirect, new AccessToken(), "/Index");
            }
            else
            {
                var token = new AccessToken
                {
                    Value = userInfo.Token,
#warning 这里处理不正确
                    Expires = DateTimeOffset.UtcNow.AddDays(7),
                };

                res = new AccessTokenResult(AccessTokenResultStatus.Success, token, "/Index");
            }
            return res;
        }

        public async ValueTask<AccessTokenResult> RequestAccessToken(AccessTokenRequestOptions options)
        {
            return await RequestAccessToken();
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            AuthenticationState res;
            ClaimsIdentity id = new ClaimsIdentity();

            if (UserInfo != default)
            {
                id.AddClaims(UserInfo.ParseToken());
            }
            ClaimsPrincipal userprincipal = new ClaimsPrincipal();
            userprincipal.AddIdentity(id);
            res = new AuthenticationState(userprincipal);
            return res;
        }

        public async Task SignInAsync(string? userName, string? password)
        {
            if (!(string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password)))
            {
                var formContent = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>(nameof(userName), userName), new KeyValuePair<string, string>(nameof(password), password) });
                var res = await _client.PostAsync("Auth", formContent);
                if (res.IsSuccessStatusCode)
                {
                    var token = await res.Content.ReadAsStringAsync();
                    UserInfo = new UserInfo(token);
                }
                else
                {
                    throw new Exception(await res.Content.ReadAsStringAsync());
                }
            }
        }

        public async Task SignOutAsync()
        {
            UserInfo = null;
        }

        public async Task RegistAsync(string? userName, string? password)
        {
            if (!(string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password)))
            {
                var formContent = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>(nameof(userName), userName), new KeyValuePair<string, string>(nameof(password), password) });
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

        public UserSessionService(IHttpClientFactory httpClientFactory)
        {
            _client = httpClientFactory.CreateClient("Anonymous");
            CaseNumber = string.Empty;
        }
    }
}
