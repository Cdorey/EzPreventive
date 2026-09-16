using System.Security.Claims;
using EzNutrition.Presentation.Reports;
using EzNutrition.Shared.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace EzNutrition.Client.Tests.Services;

/// <summary>使用真实会话解析及 policy 注册检查报告权限适配，不用报告授权接口替身。</summary>
public sealed class ReportAuthorizationTests
{
    /// <summary>按钮使用的策略和操作权限独立对应两个 JWT claim，不从医师角色或处方权限推导。</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Policies_and_report_operations_use_the_same_independent_claims(bool issue, bool print)
    {
        var context = new SessionTestContext();
        List<Claim> claims = [new("RealName", "模拟医师"), new("InstitutionName", "模拟机构"),
            new(ClaimTypes.Role, "Doctor"), new("Permission", "Prescription")];
        if (issue) claims.Add(new("Permission", "IssueReport"));
        if (print) claims.Add(new("Permission", "PrintReport"));
        await SignInAsync(context, claims);
        using var provider = Services();
        var policy = provider.GetRequiredService<IAuthorizationService>();
        var access = new ReportAuthorization(context.Session, policy);
        var state = await context.Session.GetAuthenticationStateAsync();
        Assert.Equal(issue, (await policy.AuthorizeAsync(state.User, PolicyList.IssueReport)).Succeeded);
        Assert.Equal(print, (await policy.AuthorizeAsync(state.User, PolicyList.PrintReport)).Succeeded);
        if (issue)
        {
            var actor = await access.RequireIssuerAsync();
            Assert.Equal("模拟医师", actor.Display);
            Assert.Equal("模拟机构", actor.Organization!.Display);
            Assert.Equal("test-user-id", actor.Identifier!.Value);
        }
        else await Assert.ThrowsAsync<UnauthorizedAccessException>(() => access.RequireIssuerAsync().AsTask());
        if (print) await access.RequirePrintAsync();
        else await Assert.ThrowsAsync<UnauthorizedAccessException>(() => access.RequirePrintAsync().AsTask());
        Assert.Equal(0, context.Authentication.RefreshCount);
    }

    /// <summary>有签发 claim 但缺少认证姓名时不能签发；不影响独立的打印权限。</summary>
    [Fact]
    public async Task Issuance_requires_certified_name_in_addition_to_permission()
    {
        var context = new SessionTestContext();
        await SignInAsync(context, [new("Permission", "IssueReport"), new("Permission", "PrintReport")]);
        using var provider = Services();
        var access = new ReportAuthorization(context.Session, provider.GetRequiredService<IAuthorizationService>());
        await access.RequirePrintAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => access.RequireIssuerAsync().AsTask());
    }

    /// <summary>沿用交互会话期限，访问令牌到期不新增联网检查；退出或会话到期后立即拒绝。</summary>
    [Fact]
    public async Task Existing_session_lifetime_and_sign_out_apply_without_a_new_refresh_path()
    {
        var context = new SessionTestContext();
        Claim[] claims = [new("Permission", "PrintReport")];
        await SignInAsync(context, claims);
        using var provider = Services();
        var access = new ReportAuthorization(context.Session, provider.GetRequiredService<IAuthorizationService>());
        context.Clock.Advance(TimeSpan.FromHours(1));
        await access.RequirePrintAsync();
        Assert.Equal(0, context.Authentication.RefreshCount);
        context.Clock.Advance(TimeSpan.FromDays(31));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => access.RequirePrintAsync().AsTask());
        await SignInAsync(context, claims);
        await access.RequirePrintAsync();
        await context.Session.SignOutAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => access.RequirePrintAsync().AsTask());
    }

    private static async Task SignInAsync(SessionTestContext context, IEnumerable<Claim> claims)
    {
        context.Authentication.SignIn = _ => Task.FromResult(SessionTestContext.CreateTokens(context.Clock.GetUtcNow(), additionalClaims: claims));
        await context.Session.SignInAsync("test-user", "test-password");
    }

    private static ServiceProvider Services()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore(PolicyList.RegisterPolicies);
        return services.BuildServiceProvider();
    }
}
