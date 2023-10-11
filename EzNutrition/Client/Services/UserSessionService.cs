using EzNutrition.Client.Models;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

namespace EzNutrition.Client.Services
{
    public class UserSessionService : IAccessTokenProvider
    {
        private UserInfo? userInfo;
        private HttpClient _client;

        public UserInfo? UserInfo
        {
            get => userInfo;
            set
            {
                userInfo = value;
                UserInfoChanged?.Invoke(this, new EventArgs());
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

        public event EventHandler? UserInfoChanged;

        public UserSessionService(IHttpClientFactory httpClientFactory)
        {
            _client = httpClientFactory.CreateClient("Anonymous");
            CaseNumber = string.Empty;
        }
    }
}
