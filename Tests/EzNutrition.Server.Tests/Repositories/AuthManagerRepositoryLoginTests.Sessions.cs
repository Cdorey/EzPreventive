using EzNutrition.Server.Data;
using EzNutrition.Server.Services;
using EzNutrition.Shared.Data.DTO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;

namespace EzNutrition.Server.Tests.Repositories;

public sealed partial class AuthManagerRepositoryLoginTests
{
    [Fact]
    public async Task Login_issues_short_jwt_and_stores_only_refresh_hash()
    {
        var clock = new SessionClock();
        await using var host = LoginTestHost.Create(timeProvider: clock);
        var user = await host.CreateUserAsync("session-user", "session@example.test", true);

        var tokens = await host.Repository.Login(user.UserName!, LoginTestHost.InitialPassword,
            rememberLogin: true);
        var record = await host.DbContext.RefreshTokens.AsNoTracking().SingleAsync();
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(tokens.AccessToken);

        Assert.Equal(clock.UtcNow.AddMinutes(15), tokens.AccessTokenExpiresAtUtc);
        Assert.Equal(clock.UtcNow.AddDays(7), tokens.RefreshExpiresAtUtc);
        Assert.Equal(clock.UtcNow.AddDays(30), tokens.SessionExpiresAtUtc);
        Assert.Equal(tokens.AccessTokenExpiresAtUtc.UtcDateTime, jwt.ValidTo);
        Assert.True(tokens.RememberLogin);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(tokens.RefreshToken!))),
            record.TokenHash);
        Assert.NotEqual(tokens.RefreshToken, record.TokenHash);
    }

    [Fact]
    public async Task Rotation_consumes_old_token_and_replay_revokes_the_entire_session()
    {
        await using var host = LoginTestHost.Create();
        var user = await host.CreateUserAsync("rotation-user", "rotation@example.test", true);
        var initial = await host.Repository.Login(user.UserName!, LoginTestHost.InitialPassword);
        var sessions = host.Services.GetRequiredService<AuthenticationSessionService>();

        var rotated = await sessions.RefreshAsync(initial.RefreshToken, false, initial.SessionId);

        Assert.Equal(initial.SessionId, rotated.SessionId);
        Assert.NotEqual(initial.RefreshToken, rotated.RefreshToken);
        Assert.Equal(initial.SessionExpiresAtUtc, rotated.SessionExpiresAtUtc);
        Assert.Equal(1, await host.DbContext.RefreshTokens.CountAsync(item => item.ConsumedAtUtc != null));
        Assert.True(await sessions.IsActiveAsync(initial.SessionId, user.Id));

        await Assert.ThrowsAsync<AuthenticationSessionException>(() =>
            sessions.RefreshAsync(initial.RefreshToken, false, initial.SessionId));

        Assert.False(await sessions.IsActiveAsync(initial.SessionId, user.Id));
        await Assert.ThrowsAsync<AuthenticationSessionException>(() =>
            sessions.RefreshAsync(rotated.RefreshToken, false, rotated.SessionId));
    }

    [Fact]
    public async Task Concurrent_consumers_cannot_both_rotate_the_same_token()
    {
        await using var host = LoginTestHost.Create();
        var user = await host.CreateUserAsync("concurrent-user", "concurrent@example.test", true);
        var initial = await host.Repository.Login(user.UserName!, LoginTestHost.InitialPassword);

        async Task<bool> ConsumeAsync()
        {
            await using var scope = host.Services.CreateAsyncScope();
            try
            {
                await scope.ServiceProvider.GetRequiredService<AuthenticationSessionService>()
                    .RefreshAsync(initial.RefreshToken, false, initial.SessionId);
                return true;
            }
            catch (AuthenticationSessionException)
            {
                return false;
            }
        }

        var results = await Task.WhenAll(Task.Run(ConsumeAsync), Task.Run(ConsumeAsync));

        Assert.Single(results, succeeded => succeeded);
        Assert.Equal(2, await host.DbContext.RefreshTokens.CountAsync());
        Assert.False(await host.Services.GetRequiredService<AuthenticationSessionService>()
            .IsActiveAsync(initial.SessionId, user.Id));
    }

    [Fact]
    public async Task Refresh_does_not_extend_the_absolute_session_limit()
    {
        var clock = new SessionClock();
        await using var host = LoginTestHost.Create(timeProvider: clock);
        var user = await host.CreateUserAsync("lifetime-user", "lifetime@example.test", true);
        var tokens = await host.Repository.Login(user.UserName!, LoginTestHost.InitialPassword);
        var absoluteExpiry = tokens.SessionExpiresAtUtc;
        var sessions = host.Services.GetRequiredService<AuthenticationSessionService>();
        for (var index = 0; index < 5; index++)
        {
            clock.UtcNow = clock.UtcNow.AddDays(5);
            tokens = await sessions.RefreshAsync(tokens.RefreshToken, false, tokens.SessionId);
            Assert.Equal(absoluteExpiry, tokens.SessionExpiresAtUtc);
            Assert.True(tokens.RefreshExpiresAtUtc <= absoluteExpiry);
        }

        clock.UtcNow = absoluteExpiry.AddSeconds(-20);
        tokens = await sessions.RefreshAsync(tokens.RefreshToken, false, tokens.SessionId);
        Assert.Equal(absoluteExpiry, tokens.AccessTokenExpiresAtUtc);
        clock.UtcNow = absoluteExpiry;
        await Assert.ThrowsAsync<AuthenticationSessionException>(() =>
            sessions.RefreshAsync(tokens.RefreshToken, false, tokens.SessionId));
    }

    [Theory]
    [InlineData("idle")]
    [InlineData("stamp")]
    [InlineData("locked")]
    public async Task Expiry_and_account_security_changes_prevent_refresh(string reason)
    {
        var clock = new SessionClock();
        await using var host = LoginTestHost.Create(timeProvider: clock);
        var user = await host.CreateUserAsync("invalid-user", "invalid@example.test", true);
        var tokens = await host.Repository.Login(user.UserName!, LoginTestHost.InitialPassword);
        if (reason == "idle")
        {
            clock.UtcNow = clock.UtcNow.AddDays(7);
        }
        else if (reason == "stamp")
        {
            Assert.True((await host.UserManager.UpdateSecurityStampAsync(user)).Succeeded);
        }
        else
        {
            Assert.True((await host.UserManager.SetLockoutEndDateAsync(
                user, DateTimeOffset.UtcNow.AddDays(1))).Succeeded);
        }

        await Assert.ThrowsAsync<AuthenticationSessionException>(() =>
            host.Services.GetRequiredService<AuthenticationSessionService>()
                .RefreshAsync(tokens.RefreshToken, false, tokens.SessionId));
    }

    [Fact]
    public async Task Wrong_client_or_expected_session_does_not_consume_another_sessions_token()
    {
        await using var host = LoginTestHost.Create();
        var user = await host.CreateUserAsync("binding-user", "binding@example.test", true);
        var tokens = await host.Repository.Login(user.UserName!, LoginTestHost.InitialPassword);
        var sessions = host.Services.GetRequiredService<AuthenticationSessionService>();

        await Assert.ThrowsAsync<AuthenticationSessionException>(() =>
            sessions.RefreshAsync(tokens.RefreshToken, true, tokens.SessionId));
        var mismatch = await Assert.ThrowsAsync<AuthenticationSessionException>(() =>
            sessions.RefreshAsync(tokens.RefreshToken, false, Guid.NewGuid()));
        Assert.Equal(AuthenticationErrorCodes.SessionChanged, mismatch.Code);
        await Assert.ThrowsAsync<AuthenticationSessionException>(() =>
            sessions.RevokeAsync(tokens.RefreshToken, false, Guid.NewGuid()));
        Assert.True(await sessions.IsActiveAsync(tokens.SessionId, user.Id));
        Assert.NotNull(await sessions.RefreshAsync(tokens.RefreshToken, false, tokens.SessionId));
    }

    [Fact]
    public async Task Logout_revokes_only_its_session_and_account_deletion_cascades_all_records()
    {
        await using var host = LoginTestHost.Create();
        var user = await host.CreateUserAsync("logout-user", "logout@example.test", true);
        var first = await host.Repository.Login(user.UserName!, LoginTestHost.InitialPassword);
        var second = await host.Repository.Login(user.UserName!, LoginTestHost.InitialPassword);
        var sessions = host.Services.GetRequiredService<AuthenticationSessionService>();

        await sessions.RevokeAsync(first.RefreshToken, false, first.SessionId);
        await sessions.RevokeAsync(first.RefreshToken, false, first.SessionId);

        Assert.False(await sessions.IsActiveAsync(first.SessionId, user.Id));
        Assert.True(await sessions.IsActiveAsync(second.SessionId, user.Id));
        var deletion = await host.Services.GetRequiredService<AccountDeletionService>()
            .DeleteAsync(user.Id, AccountDeletionReason.AdministratorRequested);
        Assert.True(deletion.Succeeded);
        Assert.Empty(await host.DbContext.AuthenticationSessions.AsNoTracking().ToArrayAsync());
        Assert.Empty(await host.DbContext.RefreshTokens.AsNoTracking().ToArrayAsync());
    }

    [Fact]
    public async Task Cleanup_retains_replay_history_until_the_session_expires()
    {
        var clock = new SessionClock();
        await using var host = LoginTestHost.Create(timeProvider: clock);
        var user = await host.CreateUserAsync("cleanup-user", "cleanup@example.test", true);
        var tokens = await host.Repository.Login(user.UserName!, LoginTestHost.InitialPassword);
        var sessions = host.Services.GetRequiredService<AuthenticationSessionService>();
        await sessions.RefreshAsync(tokens.RefreshToken, false, tokens.SessionId);

        Assert.Equal(0, await sessions.DeleteExpiredAsync());
        Assert.Equal(2, await host.DbContext.RefreshTokens.CountAsync());
        clock.UtcNow = clock.UtcNow.AddDays(7);
        Assert.Equal(1, await sessions.DeleteExpiredAsync());
        Assert.Empty(await host.DbContext.RefreshTokens.AsNoTracking().ToArrayAsync());
    }

    private sealed class SessionClock : TimeProvider
    {
        internal DateTimeOffset UtcNow { get; set; } = new(2026, 9, 3, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
