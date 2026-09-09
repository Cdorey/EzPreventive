using EzNutrition.Application.Reports;
using EzNutrition.Archives.Contracts.ValueObjects;
using EzNutrition.Presentation.Services;
using EzNutrition.Shared.Policies;
using Microsoft.AspNetCore.Authorization;

namespace EzNutrition.Presentation.Reports;

/// <summary>与 AuthorizeView 使用同一认证状态和 policy，保持现有前端权限语义。</summary>
public sealed class ReportAuthorization(UserSessionService session, IAuthorizationService authorization) : IReportAuthorization
{
    /// <inheritdoc />
    public async ValueTask<ActorReference> RequireIssuerAsync(CancellationToken cancellationToken = default)
    {
        await RequireAsync(PolicyList.IssueReport, cancellationToken);
        var user = session.UserInfo;
        if (user is null || string.IsNullOrWhiteSpace(user.RealName))
            throw new UnauthorizedAccessException("当前账号缺少签发所需的认证姓名。");
        return new ActorReference
        {
            Identifier = new BusinessIdentifier(new Uri("https://eznutrition.cdorey.net/identifiers/users"), user.UserId),
            Display = user.RealName,
            Organization = string.IsNullOrWhiteSpace(user.InstitutionName) ? null : new ActorReference { Display = user.InstitutionName }
        };
    }

    /// <inheritdoc />
    public ValueTask RequirePrintAsync(CancellationToken cancellationToken = default) =>
        RequireAsync(PolicyList.PrintReport, cancellationToken);

    private async ValueTask RequireAsync(string policy, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var state = await session.GetAuthenticationStateAsync();
        if (state.User.Identity?.IsAuthenticated != true
            || !(await authorization.AuthorizeAsync(state.User, policy)).Succeeded)
            throw new UnauthorizedAccessException("当前账号没有该报告操作权限。");
        cancellationToken.ThrowIfCancellationRequested();
    }
}
