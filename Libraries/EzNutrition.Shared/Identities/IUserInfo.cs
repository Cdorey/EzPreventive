namespace EzNutrition.Shared.Identities
{
    /// <summary>
    /// 定义 EzNutrition 用户自定义声明的稳定名称。
    /// </summary>
    public static class UserClaimTypes
    {
        /// <summary>表示经认证资料确认的真实姓名。</summary>
        public const string RealName = "RealName";

        /// <summary>表示用户执业、任教或就读的机构名称。</summary>
        public const string InstitutionName = "InstitutionName";
    }

    /// <summary>
    /// 表示当前已认证用户的稳定身份快照。
    /// </summary>
    public interface IUserInfo
    {
        /// <summary>获取服务端签发的稳定用户标识。</summary>
        string UserId { get; }

        /// <summary>获取登录用户名。</summary>
        string UserName { get; }

        /// <summary>获取当前令牌中的角色。</summary>
        string[] Roles { get; }

        /// <summary>获取当前令牌中的电子邮箱；令牌未提供时为空字符串。</summary>
        string Email { get; }

        /// <summary>获取可选的经认证真实姓名。</summary>
        string? RealName { get; }

        /// <summary>获取可选的执业、任教或就读机构名称。</summary>
        string? InstitutionName { get; }
    }

    /// <summary>
    /// 表示尚未建立认证身份的用户注册资料。
    /// </summary>
    public class RegistrationMessage
    {
        public string Email { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public string[] Roles { get; set; } = Array.Empty<string>();

        public string? MainPracticeInstitution { get; set; }

        public string? MainPracticeAreas { get; set; }
    }
}
