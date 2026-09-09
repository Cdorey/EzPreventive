using EzNutrition.Server.Controllers;
using EzNutrition.Server.Data;
using EzNutrition.Server.Services;
using EzNutrition.Shared.Data.DTO;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace EzNutrition.Server.Tests.Repositories;

public sealed partial class AuthManagerRepositoryLoginTests
{
    [Fact]
    public async Task Role_claim_update_preserves_legacy_duplicate_emails_and_invalidates_only_member_sessions()
    {
        await using var host = LoginTestHost.Create();
        var member = await host.CreateUserAsync("legacy-member", "member@example.test", true);
        var other = await host.CreateUserAsync("outside-role", "other@example.test", true);
        var memberTokens = await host.Repository.Login(member.UserName!, LoginTestHost.InitialPassword);
        var otherTokens = await host.Repository.Login(other.UserName!, LoginTestHost.InitialPassword);
        var roleManager = host.Services.GetRequiredService<RoleManager<IdentityRole>>();
        var role = new IdentityRole("Physician");
        Assert.True((await roleManager.CreateAsync(role)).Succeeded);
        Assert.True((await host.UserManager.AddToRoleAsync(member, role.Name!)).Succeeded);
        Assert.True((await roleManager.AddClaimAsync(role, new Claim("Permission", "Prescription"))).Succeeded);

        // 模拟已有的重复邮箱数据；正常账号编辑和注册仍须经过唯一性验证。
        other.Email = member.Email;
        other.NormalizedEmail = member.NormalizedEmail;
        await host.DbContext.SaveChangesAsync();
        var validator = host.UserManager.UserValidators.OfType<OptionalUniqueEmailUserValidator>().Single();
        Assert.Contains((await validator.ValidateAsync(host.UserManager, member)).Errors, error => error.Code == "DuplicateEmail");
        var originalStamp = member.SecurityStamp;
        var originalConcurrency = member.ConcurrencyStamp;
        var otherStamp = other.SecurityStamp;
        var passwordHash = member.PasswordHash;
        var loginTime = member.LastSuccessfulLoginAtUtc;

        var result = await ActivatorUtilities.CreateInstance<AdminController>(host.Services).UpdateRoleClaims(
            role.Name!, [new() { Type = "Permission", Value = "PrintReport" }], CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        host.DbContext.ChangeTracker.Clear();
        var saved = await host.DbContext.Users.SingleAsync(user => user.Id == member.Id);
        var untouched = await host.DbContext.Users.SingleAsync(user => user.Id == other.Id);
        Assert.NotEqual(originalStamp, saved.SecurityStamp);
        Assert.NotEqual(originalConcurrency, saved.ConcurrencyStamp);
        Assert.Equal(otherStamp, untouched.SecurityStamp);
        Assert.Equal(member.Email, saved.Email);
        Assert.Equal(member.NormalizedEmail, saved.NormalizedEmail);
        Assert.Equal(passwordHash, saved.PasswordHash);
        Assert.Equal(loginTime, saved.LastSuccessfulLoginAtUtc);
        Assert.Equal("PrintReport", (await host.DbContext.RoleClaims.SingleAsync()).ClaimValue);
        Assert.Contains((await validator.ValidateAsync(host.UserManager, saved)).Errors, error => error.Code == "DuplicateEmail");

        var sessions = host.Services.GetRequiredService<AuthenticationSessionService>();
        var rejected = await Assert.ThrowsAsync<AuthenticationSessionException>(() =>
            sessions.RefreshAsync(memberTokens.RefreshToken, false, memberTokens.SessionId));
        Assert.Equal(AuthenticationErrorCodes.SessionInvalid, rejected.Code);
        var refreshed = await sessions.RefreshAsync(otherTokens.RefreshToken, false, otherTokens.SessionId);
        Assert.Equal(otherTokens.SessionId, refreshed.SessionId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Role_claim_update_rolls_back_claims_and_stamps_when_member_write_fails(bool concurrentChange)
    {
        await using var host = LoginTestHost.Create();
        var member = await host.CreateUserAsync("member", "member@example.test", true);
        var role = new IdentityRole("Physician") { NormalizedName = "PHYSICIAN" };
        host.DbContext.Roles.Add(role);
        host.DbContext.UserRoles.Add(new IdentityUserRole<string> { RoleId = role.Id, UserId = member.Id });
        host.DbContext.RoleClaims.Add(new IdentityRoleClaim<string>
        { RoleId = role.Id, ClaimType = "Permission", ClaimValue = "Prescription" });
        await host.DbContext.SaveChangesAsync();
        var originalStamp = member.SecurityStamp;
        if (concurrentChange)
        {
            // 保留当前上下文的旧版本，模拟另一请求在成员读取后更新了账号。
            await host.DbContext.Users.Where(user => user.Id == member.Id)
                .ExecuteUpdateAsync(update => update.SetProperty(user => user.ConcurrencyStamp, "concurrent-version"));
        }
        else
        {
            await host.DbContext.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER reject_stamp_update BEFORE UPDATE OF SecurityStamp ON AspNetUsers
                BEGIN SELECT RAISE(ABORT, 'Synthetic storage failure'); END;
                """);
        }

        var result = Assert.IsAssignableFrom<ObjectResult>(await ActivatorUtilities.CreateInstance<AdminController>(host.Services)
            .UpdateRoleClaims(role.Name!, [new() { Type = "Permission", Value = "PrintReport" }], CancellationToken.None));
        Assert.Equal(concurrentChange ? 409 : 500, result.StatusCode);
        var error = Assert.IsType<AccountOperationResultDto>(result.Value);
        Assert.False(error.Success);
        Assert.Contains("本次保存已撤销", error.Message);
        host.DbContext.ChangeTracker.Clear();
        Assert.Equal("Prescription", (await host.DbContext.RoleClaims.SingleAsync()).ClaimValue);
        var saved = await host.DbContext.Users.SingleAsync();
        Assert.Equal(originalStamp, saved.SecurityStamp);
        if (concurrentChange) Assert.Equal("concurrent-version", saved.ConcurrencyStamp);
    }

    [Theory]
    [InlineData("Permission", "", "不能为空")]
    [InlineData("EzNutrition.SecurityStamp", "forged", "系统身份声明")]
    public async Task Invalid_role_claims_return_a_user_facing_reason(string type, string value, string reason)
    {
        await using var host = LoginTestHost.Create();
        var result = Assert.IsType<BadRequestObjectResult>(await ActivatorUtilities.CreateInstance<AdminController>(host.Services)
            .UpdateRoleClaims("Physician", [new() { Type = type, Value = value }], CancellationToken.None));
        var error = Assert.IsType<AccountOperationResultDto>(result.Value);
        Assert.False(error.Success);
        Assert.Contains(reason, error.Message);
    }
}
