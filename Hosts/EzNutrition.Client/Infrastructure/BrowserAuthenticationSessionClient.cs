using EzNutrition.Presentation.Services;
using EzNutrition.Shared.Data.DTO;
using Microsoft.JSInterop;

namespace EzNutrition.Client.Infrastructure;

/// <summary>把浏览器凭据交给 HttpOnly Cookie 管理，共享会话层仅接收短期访问令牌。</summary>
public sealed class BrowserAuthenticationSessionClient(
    IJSRuntime js,
    ApplicationServerEndpoint endpoint) : IAuthenticationSessionClient, IAsyncDisposable
{
    private readonly object moduleLock = new();
    private Task<IJSObjectReference>? moduleTask;
    private DotNetObjectReference<BrowserAuthenticationSessionClient>? reference;

    /// <summary>其他标签页登录或退出时通知共享会话层。</summary>
    public event Func<Task>? SessionChanged;

    /// <inheritdoc />
    public bool CanRememberLogin => true;

    /// <inheritdoc />
    public async Task<AuthenticationTokensDto> SignInAsync(
        LoginRequestDto request, CancellationToken cancellationToken = default) =>
        await InvokeAsync("login", request, cancellationToken) ?? throw SessionInvalid();

    /// <inheritdoc />
    public Task<AuthenticationTokensDto?> RestoreAsync(CancellationToken cancellationToken = default) =>
        InvokeAsync("restore", new RefreshRequestDto(), cancellationToken);

    /// <inheritdoc />
    public async Task<AuthenticationTokensDto> RefreshAsync(
        Guid sessionId, CancellationToken cancellationToken = default) =>
        await InvokeAsync("refresh", new RefreshRequestDto { SessionId = sessionId }, cancellationToken)
            ?? throw SessionInvalid();

    /// <inheritdoc />
    public async Task SignOutAsync(Guid? sessionId, CancellationToken cancellationToken = default) =>
        _ = await InvokeAsync("logout", new RefreshRequestDto { SessionId = sessionId }, cancellationToken);

    /// <summary>接收不包含凭据的跨标签页通知。</summary>
    [JSInvokable]
    public Task NotifySessionChanged() => SessionChanged?.Invoke() ?? Task.CompletedTask;

    private async Task<AuthenticationTokensDto?> InvokeAsync(
        string method, object request, CancellationToken cancellationToken)
    {
        Task<IJSObjectReference> loading;
        lock (moduleLock)
        {
            loading = moduleTask ??= LoadModuleAsync();
        }
        BrowserResponse response;
        try
        {
            var module = await loading;
            response = await module.InvokeAsync<BrowserResponse>(
                method, cancellationToken, endpoint.BaseAddress.AbsoluteUri, request);
        }
        catch (JSException exception)
        {
            lock (moduleLock)
            {
                if (moduleTask?.IsFaulted == true)
                {
                    moduleTask = null;
                }
            }
            throw new InvalidOperationException("无法使用浏览器登录服务，请重新加载后重试。", exception);
        }
        if (response.Status == 204)
        {
            return null;
        }
        if (response.Status == 200 && response.Tokens is not null)
        {
            return response.Tokens;
        }
        if (response.Status is 401 or 409)
        {
            throw new SessionAuthenticationException(
                response.Error?.Code ?? AuthenticationErrorCodes.SessionInvalid,
                response.Error?.Message ?? "登录会话已失效，请重新登录。");
        }
        throw new InvalidOperationException(response.Error?.Message ?? "认证服务暂时不可用，请稍后重试。");
    }

    private async Task<IJSObjectReference> LoadModuleAsync()
    {
        var module = await js.InvokeAsync<IJSObjectReference>("import", "./auth-session.mjs");
        reference = DotNetObjectReference.Create(this);
        await module.InvokeVoidAsync("subscribe", endpoint.BaseAddress.AbsoluteUri, reference);
        return module;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (moduleTask is { IsCompletedSuccessfully: true })
        {
            var module = await moduleTask;
            await module.InvokeVoidAsync("unsubscribe", endpoint.BaseAddress.AbsoluteUri);
            await module.DisposeAsync();
        }
        reference?.Dispose();
    }

    private static SessionAuthenticationException SessionInvalid() =>
        new(AuthenticationErrorCodes.SessionInvalid, "登录会话已失效，请重新登录。");

    private sealed class BrowserResponse
    {
        public int Status { get; set; }
        public AuthenticationTokensDto? Tokens { get; set; }
        public AuthenticationErrorDto? Error { get; set; }
    }
}
