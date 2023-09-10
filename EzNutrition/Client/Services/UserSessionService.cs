using EzNutrition.Client.Models;

namespace EzNutrition.Client.Services
{
    public class UserSessionService
    {
        private UserInfo? userInfo;


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

        public async Task GetSystemInfoAsync(HttpClient httpClient)
        {
            CaseNumber = await httpClient.GetStringAsync("SystemInfo/CaseNumber/");
        }

        public event EventHandler? UserInfoChanged;

        public UserSessionService()
        {
            CaseNumber = string.Empty;
        }
    }
}
