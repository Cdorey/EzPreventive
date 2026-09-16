using EzNutrition.Server.Data.Repositories;
using EzNutrition.Server.Services;
using EzNutrition.Shared.Data.DTO;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EzNutrition.Server.Controllers;

/// <summary>同源浏览器认证接口；刷新凭据仅通过受保护的 Cookie 传递。</summary>
[ApiController]
[Route("Auth/Browser")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[AutoValidateAntiforgeryToken]
[TypeFilter(typeof(AuthenticationExceptionFilter))]
public sealed class BrowserAuthController(
    AuthManagerRepository accounts,
    AuthenticationSessionService sessions,
    IAntiforgery antiforgery,
    IWebHostEnvironment environment) : ControllerBase
{
    internal const string RefreshCookieName = "EzNutrition.Refresh";

    /// <summary>取得当前浏览器的防伪请求令牌；刷新凭据始终保持 HttpOnly。</summary>
    [HttpGet("Csrf")]
    public IActionResult Csrf() => Ok(new
    {
        requestToken = antiforgery.GetAndStoreTokens(HttpContext).RequestToken
    });

    /// <summary>登录并设置刷新 Cookie，响应正文仅返回短期访问令牌及期限。</summary>
    [HttpPost("Login")]
    [EnableRateLimiting("Login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequestDto request, CancellationToken cancellationToken)
    {
        var tokens = await accounts.Login(
            request.UserName, request.Password, isBrowser: true,
            request.RememberLogin, cancellationToken);
        await sessions.RevokeAsync(
            Request.Cookies[RefreshCookieName], isBrowser: true,
            expectedSessionId: null, cancellationToken);
        SetRefreshCookie(tokens);
        return Ok(tokens with { RefreshToken = null });
    }

    /// <summary>使用 Cookie 恢复或刷新会话；不存在 Cookie 时返回空结果。</summary>
    [HttpPost("Refresh")]
    [EnableRateLimiting("Refresh")]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshRequestDto request, CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[RefreshCookieName];
        if (string.IsNullOrEmpty(refreshToken))
        {
            return NoContent();
        }

        try
        {
            var tokens = await sessions.RefreshAsync(
                refreshToken, isBrowser: true, request.SessionId, cancellationToken);
            SetRefreshCookie(tokens);
            return Ok(tokens with { RefreshToken = null });
        }
        catch (AuthenticationSessionException exception)
            when (exception.Code == AuthenticationErrorCodes.SessionInvalid)
        {
            Response.Cookies.Delete(RefreshCookieName, CreateCookieOptions());
            throw;
        }
    }

    /// <summary>撤销预期会话并清除 Cookie，避免旧标签页退出其他账号的新会话。</summary>
    [HttpPost("Logout")]
    [EnableRateLimiting("Refresh")]
    public async Task<IActionResult> Logout(
        [FromBody] RefreshRequestDto request, CancellationToken cancellationToken)
    {
        await sessions.RevokeAsync(
            Request.Cookies[RefreshCookieName], isBrowser: true,
            request.SessionId, cancellationToken);
        Response.Cookies.Delete(RefreshCookieName, CreateCookieOptions());
        return NoContent();
    }

    private void SetRefreshCookie(AuthenticationTokensDto tokens)
    {
        var cookie = CreateCookieOptions();
        if (tokens.RememberLogin)
        {
            cookie.Expires = tokens.SessionExpiresAtUtc;
        }

        Response.Cookies.Append(RefreshCookieName, tokens.RefreshToken!, cookie);
    }

    private CookieOptions CreateCookieOptions() => new()
    {
        HttpOnly = true,
        Secure = !environment.IsDevelopment() || Request.IsHttps,
        SameSite = SameSiteMode.Strict,
        Path = Request.PathBase.Add("/Auth/Browser").Value!,
        IsEssential = true
    };
}
