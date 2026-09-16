namespace EzNutrition.Server.Data.Entities;

/// <summary>刷新凭据的单次使用记录；仅持久化哈希以降低数据库泄露的影响。</summary>
public sealed class RefreshTokenRecord
{
    /// <summary>获取或设置记录标识。</summary>
    public Guid Id { get; set; }

    /// <summary>获取或设置所属会话。</summary>
    public Guid SessionId { get; set; }

    /// <summary>获取或设置刷新凭据的 SHA-256 十六进制哈希。</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>获取或设置签发时间。</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>获取或设置消费时间；已消费记录用于识别重放。</summary>
    public DateTime? ConsumedAtUtc { get; set; }

    /// <summary>获取或设置所属登录会话实体。</summary>
    public AuthenticationSession Session { get; set; } = null!;
}
