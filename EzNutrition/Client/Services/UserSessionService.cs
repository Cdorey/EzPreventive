using EzNutrition.Client.Models;

namespace EzNutrition.Client.Services
{
    public class UserSessionService
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

        public event EventHandler? UserInfoChanged;

        public UserSessionService(IHttpClientFactory httpClientFactory)
        {
            _client = httpClientFactory.CreateClient("Anonymous");
            CaseNumber = string.Empty;
        }
    }
}
