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

        public event EventHandler? UserInfoChanged;

        public UserSessionService() { }
    }
}
