using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace EzNutrition.Shared.Data.DTO;

/// <summary>使用账号密码建立新的登录会话。</summary>
public sealed class LoginRequestDto
{
    /// <summary>获取或设置登录用户名。</summary>
    [Required, StringLength(256)]
    public string UserName { get; set; } = string.Empty;

    /// <summary>获取或设置密码；仅用于本次登录，不应持久化。</summary>
    [Required, StringLength(4096)]
    public string Password { get; set; } = string.Empty;

    /// <summary>获取或设置是否允许宿主在重启后恢复此会话。</summary>
    public bool RememberLogin { get; set; }
}

/// <summary>刷新或撤销当前会话；浏览器由 HttpOnly Cookie 携带刷新凭据。</summary>
public sealed class RefreshRequestDto
{
    /// <summary>获取或设置预期会话，防止其他窗口切换账号后误用新的身份。</summary>
    public Guid? SessionId { get; set; }

    /// <summary>获取或设置桌面客户端的刷新令牌。</summary>
    [StringLength(128)]
    public string? RefreshToken { get; set; }
}

/// <summary>登录或刷新成功后的凭据及其服务器有效期。</summary>
public sealed record AuthenticationTokensDto
{
    /// <summary>获取当前登录会话标识；轮换期间保持不变。</summary>
    public Guid SessionId { get; init; }

    /// <summary>获取访问业务接口所需的短期 JWT。</summary>
    public string AccessToken { get; init; } = string.Empty;

    /// <summary>获取访问令牌的 UTC 到期时间。</summary>
    public DateTimeOffset AccessTokenExpiresAtUtc { get; init; }

    /// <summary>获取本次刷新凭据的 UTC 到期时间。</summary>
    public DateTimeOffset RefreshExpiresAtUtc { get; init; }

    /// <summary>获取会话的绝对 UTC 到期时间；轮换不会延长此期限。</summary>
    public DateTimeOffset SessionExpiresAtUtc { get; init; }

    /// <summary>获取用户是否选择持久化此会话。</summary>
    public bool RememberLogin { get; init; }

    /// <summary>获取桌面端刷新凭据；浏览器响应省略此字段并改用 HttpOnly Cookie。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RefreshToken { get; init; }
}

/// <summary>认证失败的稳定错误码及可向用户显示的说明。</summary>
/// <param name="Code">供客户端决定恢复或重新登录的错误码。</param>
/// <param name="Message">不包含凭据内容的说明。</param>
public sealed record AuthenticationErrorDto(string Code, string Message);

/// <summary>认证接口和 JWT 挑战共用的错误码。</summary>
public static class AuthenticationErrorCodes
{
    /// <summary>账号密码或账户状态不允许登录。</summary>
    public const string InvalidCredentials = "invalid_credentials";

    /// <summary>访问令牌过期，可以尝试使用刷新凭据。</summary>
    public const string AccessTokenExpired = "access_token_expired";

    /// <summary>登录会话已过期、撤销或不再符合账户安全状态。</summary>
    public const string SessionInvalid = "session_invalid";

    /// <summary>共享凭据所属会话已改变，当前操作必须停止。</summary>
    public const string SessionChanged = "session_changed";
}
