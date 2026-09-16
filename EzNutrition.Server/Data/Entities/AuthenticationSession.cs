namespace EzNutrition.Server.Data.Entities;

/// <summary>一次独立登录的服务端会话；所属刷新令牌共用此撤销边界。</summary>
public sealed class AuthenticationSession
{
    /// <summary>获取或设置稳定会话标识。</summary>
    public Guid Id { get; set; }

    /// <summary>获取或设置 Identity 用户标识。</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>获取或设置建立会话时的安全戳指纹。</summary>
    public string SecurityStampFingerprint { get; set; } = string.Empty;

    /// <summary>获取或设置是否使用浏览器 Cookie 传递刷新凭据。</summary>
    public bool IsBrowser { get; set; }

    /// <summary>获取或设置是否允许客户端持久化登录。</summary>
    public bool RememberLogin { get; set; }

    /// <summary>获取或设置会话创建时间。</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>获取或设置闲置到期时间。</summary>
    public DateTime RefreshExpiresAtUtc { get; set; }

    /// <summary>获取或设置不可延长的绝对到期时间。</summary>
    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>获取或设置撤销时间；空值表示尚未撤销。</summary>
    public DateTime? RevokedAtUtc { get; set; }

    /// <summary>获取或设置并发版本，防止轮换覆盖退出或另一次轮换。</summary>
    public Guid Version { get; set; }
}
