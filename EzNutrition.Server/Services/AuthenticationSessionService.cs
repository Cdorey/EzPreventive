using EzNutrition.Server.Data;
using EzNutrition.Server.Data.Entities;
using EzNutrition.Server.Services.Settings;
using EzNutrition.Shared.Data.DTO;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;

namespace EzNutrition.Server.Services;

/// <summary>管理登录会话、一次性刷新令牌和会话撤销。</summary>
public sealed class AuthenticationSessionService(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    JwtService jwtService,
    IOptions<JwtSettings> options,
    TimeProvider timeProvider)
{
    /// <summary>为已完成密码验证的用户建立独立会话。</summary>
    public async Task<AuthenticationTokensDto> CreateAsync(
        ApplicationUser user,
        bool isBrowser,
        bool rememberLogin,
        CancellationToken cancellationToken = default)
    {
        var now = GetUtcNow();
        var session = new AuthenticationSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SecurityStampFingerprint = JwtService.CreateSecurityStampFingerprint(
                await userManager.GetSecurityStampAsync(user)),
            IsBrowser = isBrowser,
            RememberLogin = rememberLogin,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(options.Value.SessionLifetimeDays),
            Version = Guid.NewGuid()
        };
        session.RefreshExpiresAtUtc = Earlier(
            now.AddDays(options.Value.RefreshIdleDays), session.ExpiresAtUtc);
        var refreshToken = CreateRefreshToken();
        var result = await CreateResponseAsync(user, session, refreshToken, now);
        db.AuthenticationSessions.Add(session);
        db.RefreshTokens.Add(CreateRecord(session.Id, refreshToken, now));
        await db.SaveChangesAsync(cancellationToken);
        return result;
    }

    /// <summary>
    /// 消费刷新凭据并签发新凭据；同一凭据重复使用会撤销所属会话。
    /// </summary>
    /// <remarks>会话版本更新、旧凭据消费和新凭据写入共用一个数据库事务。</remarks>
    public async Task<AuthenticationTokensDto> RefreshAsync(
        string? refreshToken,
        bool isBrowser,
        Guid? expectedSessionId,
        CancellationToken cancellationToken = default)
    {
        var record = await FindAsync(refreshToken, cancellationToken)
            ?? throw new AuthenticationSessionException();
        var session = record.Session;
        CheckClient(session, isBrowser, expectedSessionId);
        var now = GetUtcNow();
        if (record.ConsumedAtUtc is not null)
        {
            await RevokeSessionAsync(session.Id, cancellationToken);
            throw new AuthenticationSessionException();
        }

        var user = await userManager.FindByIdAsync(session.UserId);
        if (!IsActive(session, now) || user is null ||
            (userManager.SupportsUserLockout && await userManager.IsLockedOutAsync(user)) ||
            (!string.IsNullOrWhiteSpace(user.Email) && !user.EmailConfirmed) ||
            !JwtService.IsSecurityStampFingerprintValid(
                session.SecurityStampFingerprint, await userManager.GetSecurityStampAsync(user)))
        {
            await RevokeSessionAsync(session.Id, cancellationToken);
            throw new AuthenticationSessionException();
        }

        var oldVersion = session.Version;
        session.Version = Guid.NewGuid();
        session.RefreshExpiresAtUtc = Earlier(
            now.AddDays(options.Value.RefreshIdleDays), session.ExpiresAtUtc);
        var nextToken = CreateRefreshToken();
        var result = await CreateResponseAsync(user, session, nextToken, now);
        var rotated = false;
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            // 条件更新在数据库中竞争同一行，跨服务实例也只能有一个请求成功。
            var changed = await db.AuthenticationSessions
                .Where(item => item.Id == session.Id && item.Version == oldVersion &&
                    item.RevokedAtUtc == null && item.RefreshExpiresAtUtc > now &&
                    item.ExpiresAtUtc > now)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(item => item.Version, session.Version)
                    .SetProperty(item => item.RefreshExpiresAtUtc, session.RefreshExpiresAtUtc),
                    cancellationToken);
            if (changed == 1)
            {
                await db.RefreshTokens.Where(item => item.Id == record.Id)
                    .ExecuteUpdateAsync(update => update.SetProperty(
                        item => item.ConsumedAtUtc, (DateTime?)now), cancellationToken);
                db.RefreshTokens.Add(CreateRecord(session.Id, nextToken, now));
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                rotated = true;
            }
            else
            {
                await transaction.RollbackAsync(cancellationToken);
            }
        }

        if (!rotated)
        {
            await RevokeSessionAsync(session.Id, cancellationToken);
            throw new AuthenticationSessionException();
        }

        return result;
    }

    /// <summary>凭刷新令牌撤销整个会话；重复退出或未知令牌均可安全完成。</summary>
    public async Task RevokeAsync(
        string? refreshToken,
        bool isBrowser,
        Guid? expectedSessionId,
        CancellationToken cancellationToken = default)
    {
        var record = await FindAsync(refreshToken, cancellationToken);
        if (record is null)
        {
            return;
        }

        CheckClient(record.Session, isBrowser, expectedSessionId);
        await RevokeSessionAsync(record.SessionId, cancellationToken);
    }

    /// <summary>验证业务请求的会话归属、撤销状态和期限。</summary>
    public Task<bool> IsActiveAsync(
        Guid sessionId, string userId, CancellationToken cancellationToken = default)
    {
        var now = GetUtcNow();
        return db.AuthenticationSessions.AsNoTracking().AnyAsync(
            item => item.Id == sessionId && item.UserId == userId &&
                item.RevokedAtUtc == null && item.RefreshExpiresAtUtc > now &&
                item.ExpiresAtUtc > now, cancellationToken);
    }

    /// <summary>清理已到期的会话及其刷新记录；有效会话的消费历史会一直保留。</summary>
    public Task<int> DeleteExpiredAsync(CancellationToken cancellationToken = default)
    {
        var now = GetUtcNow();
        return db.AuthenticationSessions
            .Where(item => item.RefreshExpiresAtUtc <= now || item.ExpiresAtUtc <= now)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private Task<RefreshTokenRecord?> FindAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 128)
        {
            return Task.FromResult<RefreshTokenRecord?>(null);
        }

        var hash = HashToken(token);
        return db.RefreshTokens.AsNoTracking().Include(item => item.Session)
            .SingleOrDefaultAsync(item => item.TokenHash == hash, cancellationToken);
    }

    private async Task RevokeSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var now = GetUtcNow();
        var version = Guid.NewGuid();
        await db.AuthenticationSessions.Where(item => item.Id == sessionId && item.RevokedAtUtc == null)
            .ExecuteUpdateAsync(update => update
                .SetProperty(item => item.RevokedAtUtc, (DateTime?)now)
                .SetProperty(item => item.Version, version), cancellationToken);
    }

    private async Task<AuthenticationTokensDto> CreateResponseAsync(
        ApplicationUser user, AuthenticationSession session, string refreshToken, DateTime now)
    {
        var expires = Earlier(now.AddMinutes(options.Value.AccessTokenMinutes), session.ExpiresAtUtc);
        return new AuthenticationTokensDto
        {
            SessionId = session.Id,
            AccessToken = await jwtService.GenerateJwtToken(
                user, session.Id, session.SecurityStampFingerprint, now, expires),
            AccessTokenExpiresAtUtc = new DateTimeOffset(expires),
            RefreshToken = refreshToken,
            RefreshExpiresAtUtc = new DateTimeOffset(session.RefreshExpiresAtUtc),
            SessionExpiresAtUtc = new DateTimeOffset(session.ExpiresAtUtc),
            RememberLogin = session.RememberLogin
        };
    }

    private static RefreshTokenRecord CreateRecord(Guid sessionId, string token, DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        SessionId = sessionId,
        TokenHash = HashToken(token),
        CreatedAtUtc = now
    };

    private static void CheckClient(AuthenticationSession session, bool isBrowser, Guid? expectedSessionId)
    {
        if (session.IsBrowser != isBrowser)
        {
            throw new AuthenticationSessionException();
        }

        if (expectedSessionId is not null && expectedSessionId != session.Id)
        {
            throw new AuthenticationSessionException(AuthenticationErrorCodes.SessionChanged);
        }
    }

    private static bool IsActive(AuthenticationSession session, DateTime now) =>
        session.RevokedAtUtc is null && session.RefreshExpiresAtUtc > now && session.ExpiresAtUtc > now;

    private DateTime GetUtcNow() =>
        DateTimeOffset.FromUnixTimeSeconds(timeProvider.GetUtcNow().ToUnixTimeSeconds()).UtcDateTime;

    private static DateTime Earlier(DateTime first, DateTime second) => first <= second ? first : second;

    private static string CreateRefreshToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

/// <summary>表示凭据已失效或共享登录会话已被其他窗口替换。</summary>
public sealed class AuthenticationSessionException(
    string code = AuthenticationErrorCodes.SessionInvalid)
    : Exception(code == AuthenticationErrorCodes.SessionChanged
        ? "登录状态已在其他窗口改变，请重新确认当前账号。"
        : "登录会话已失效，请重新登录。")
{
    /// <summary>获取可稳定识别的认证错误码。</summary>
    public string Code { get; } = code;
}
