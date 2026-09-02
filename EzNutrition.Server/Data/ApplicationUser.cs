using Microsoft.AspNetCore.Identity;

namespace EzNutrition.Server.Data;

/// <summary>
/// 表示 EzNutrition 使用的 Identity 用户，并承载账号生命周期信息。
/// </summary>
public sealed class ApplicationUser : IdentityUser
{
    /// <summary>
    /// 获取或设置账号创建的 UTC 时间。
    /// </summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 获取或设置最近一次成功登录并签发访问令牌的 UTC 时间。
    /// </summary>
    public DateTime? LastSuccessfulLoginAtUtc { get; set; }
}
