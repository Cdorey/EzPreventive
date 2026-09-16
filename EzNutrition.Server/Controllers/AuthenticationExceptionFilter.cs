using EzNutrition.Server.Services;
using EzNutrition.Shared.Data.DTO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EzNutrition.Server.Controllers;

/// <summary>将已知的认证失败统一映射为稳定 HTTP 错误，不暴露内部异常。</summary>
public sealed class AuthenticationExceptionFilter : ExceptionFilterAttribute
{
    /// <inheritdoc />
    public override void OnException(ExceptionContext context)
    {
        var error = context.Exception switch
        {
            AuthenticationSessionException session => new AuthenticationErrorDto(session.Code, session.Message),
            UnauthorizedAccessException => new AuthenticationErrorDto(
                AuthenticationErrorCodes.InvalidCredentials,
                "用户名或密码错误、邮箱尚未确认，或账户暂时被锁定。"),
            _ => null
        };
        if (error is null)
        {
            return;
        }

        context.Result = new ObjectResult(error)
        {
            StatusCode = error.Code == AuthenticationErrorCodes.SessionChanged
                ? StatusCodes.Status409Conflict
                : StatusCodes.Status401Unauthorized
        };
        context.ExceptionHandled = true;
    }
}
