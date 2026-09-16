using EzNutrition.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace EzNutrition.Server.Tests.Repositories;

public sealed partial class AuthManagerRepositoryLoginTests
{
    // 记录当前登录行为，避免把角色权限保存的修复误认为同时修复了重复邮箱登录。
    [Theory]
    [InlineData("legacy-one", 0)]
    [InlineData("legacy-two", 0)]
    [InlineData("legacy-one", 2)]
    public async Task Legacy_duplicate_email_blocks_correct_password_login_before_session_creation(
        string userName, int failedCount)
    {
        await using var host = LoginTestHost.Create();
        var users = await CreateLegacyDuplicateEmailUsersAsync(host);
        var selected = users.Single(user => user.UserName == userName);
        selected.AccessFailedCount = failedCount;
        await host.DbContext.SaveChangesAsync();

        if (failedCount == 0)
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                host.Repository.Login(userName, LoginTestHost.InitialPassword));
            Assert.Equal("Failed to record the successful login time.", error.Message);
        }
        else
        {
            // Identity 重置失败计数也会校验用户，失败后不会进入记录登录时间的分支。
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                host.Repository.Login(userName, LoginTestHost.InitialPassword));
        }

        host.DbContext.ChangeTracker.Clear();
        Assert.Empty(await host.DbContext.AuthenticationSessions.ToListAsync());
        Assert.Empty(await host.DbContext.RefreshTokens.ToListAsync());
        var persisted = await host.DbContext.Users.SingleAsync(user => user.Id == selected.Id);
        Assert.Null(persisted.LastSuccessfulLoginAtUtc);
        Assert.Equal(failedCount, persisted.AccessFailedCount);
    }

    [Fact]
    public async Task Legacy_duplicate_email_prevents_persisting_wrong_password_failure_count()
    {
        await using var host = LoginTestHost.Create();
        var selected = (await CreateLegacyDuplicateEmailUsersAsync(host))[0];

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            host.Repository.Login(selected.UserName!, "wrong-password"));

        host.DbContext.ChangeTracker.Clear();
        var persisted = await host.DbContext.Users.SingleAsync(user => user.Id == selected.Id);
        Assert.Equal(0, persisted.AccessFailedCount);
        Assert.Null(persisted.LockoutEnd);
        Assert.Empty(await host.DbContext.AuthenticationSessions.ToListAsync());
    }

    /// <summary>模拟旧数据中的邮箱冲突，不通过真实注册接口制造新的重复账号。</summary>
    private static async Task<ApplicationUser[]> CreateLegacyDuplicateEmailUsersAsync(LoginTestHost host)
    {
        var first = await host.CreateUserAsync("legacy-one", "one@example.test", true);
        var second = await host.CreateUserAsync("legacy-two", "two@example.test", true);
        second.Email = first.Email;
        second.NormalizedEmail = first.NormalizedEmail;
        await host.DbContext.SaveChangesAsync();
        return [first, second];
    }
}
