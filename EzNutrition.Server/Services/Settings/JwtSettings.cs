namespace EzNutrition.Server.Services.Settings
{
    /// <summary>JWT 签名配置及登录会话有效期。</summary>
    public class JwtSettings
    {
        /// <summary>获取或设置 Base64 编码的 RSA 公钥。</summary>
        [System.ComponentModel.DataAnnotations.Required]
        public string PublicKey { get; set; } = string.Empty;

        /// <summary>获取或设置 Base64 编码的 RSA 私钥。</summary>
        [System.ComponentModel.DataAnnotations.Required]
        public string PrivateKey { get; set; } = string.Empty;

        /// <summary>获取或设置访问令牌有效分钟数。</summary>
        [System.ComponentModel.DataAnnotations.Range(2, 60)]
        public int AccessTokenMinutes { get; set; } = 15;

        /// <summary>获取或设置连续未刷新多少天后会话失效。</summary>
        [System.ComponentModel.DataAnnotations.Range(1, 90)]
        public int RefreshIdleDays { get; set; } = 7;

        /// <summary>获取或设置登录会话的最长天数。</summary>
        [System.ComponentModel.DataAnnotations.Range(1, 90)]
        public int SessionLifetimeDays { get; set; } = 30;
    }
}
