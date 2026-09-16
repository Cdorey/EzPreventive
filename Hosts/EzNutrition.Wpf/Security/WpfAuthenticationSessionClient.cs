using EzNutrition.Presentation.Services;
using EzNutrition.Presentation.Models;
using EzNutrition.Shared.Data.DTO;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace EzNutrition.Wpf.Security;

/// <summary>通过独立 HTTP 客户端和端点级 DPAPI 存储管理桌面登录会话。</summary>
internal sealed class WpfAuthenticationSessionClient(
    IHttpClientFactory httpClientFactory,
    DpapiLoginCredentialStore store,
    ILogger<WpfAuthenticationSessionClient> logger) : IAuthenticationSessionClient
{
    private readonly HttpClient client = httpClientFactory.CreateClient("Authentication");
    private AuthenticationTokensDto? current;

    /// <inheritdoc />
    public bool CanRememberLogin => true;

    /// <inheritdoc />
    public async Task<AuthenticationTokensDto> SignInAsync(
        LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        await using var operationLock = await store.AcquireLockAsync(cancellationToken);
        var previousInMemory = current;
        SavedRefreshSession? previous;
        try
        {
            previous = await store.ReadAsync(cancellationToken);
        }
        catch (InvalidDataException)
        {
            // 明确重新登录可以替换损坏的本机副本；不尝试从损坏内容恢复凭据。
            previous = null;
        }
        using var response = await SendAsync("Auth/Login", request, cancellationToken);
        var tokens = await ReadTokensAsync(response, cancellationToken);
        await AcceptAsync(tokens, clearSavedLogin: true, cancellationToken);
        if (previous is not null && previous.SessionId != tokens.SessionId)
        {
            await TryRevokeAsync(previous);
        }
        if (previousInMemory is not null && previousInMemory.SessionId != tokens.SessionId &&
            previousInMemory.SessionId != previous?.SessionId)
        {
            await TryRevokeAsync(ToCredential(previousInMemory));
        }
        return tokens with { RefreshToken = null };
    }

    /// <inheritdoc />
    public async Task<AuthenticationTokensDto?> RestoreAsync(CancellationToken cancellationToken = default)
    {
        await using var operationLock = await store.AcquireLockAsync(cancellationToken);
        var saved = await store.ReadAsync(cancellationToken);
        if (saved is null)
        {
            return null;
        }
        if (saved.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            await store.ClearAsync(cancellationToken);
            return null;
        }
        return await RefreshCredentialAsync(saved, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AuthenticationTokensDto> RefreshAsync(
        Guid sessionId, CancellationToken cancellationToken = default)
    {
        await using var operationLock = await store.AcquireLockAsync(cancellationToken);
        var saved = current is { RememberLogin: false } inMemory
            ? ToCredential(inMemory)
            : await store.ReadAsync(cancellationToken);
        if (saved is null)
        {
            throw SessionInvalid();
        }
        if (saved.SessionId != sessionId)
        {
            throw new SessionAuthenticationException(
                AuthenticationErrorCodes.SessionChanged, "其他窗口已切换账号，请重新确认登录。");
        }
        return await RefreshCredentialAsync(saved, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SignOutAsync(Guid? sessionId, CancellationToken cancellationToken = default)
    {
        await using var operationLock = await store.AcquireLockAsync(CancellationToken.None);
        SavedRefreshSession? saved = null;
        try
        {
            saved = await store.ReadAsync(CancellationToken.None);
            var credential = current is not null && (sessionId is null || current.SessionId == sessionId)
                ? ToCredential(current) : saved;
            if (credential is not null && (sessionId is null || credential.SessionId == sessionId))
            {
                using var response = await SendAsync("Auth/Logout", new RefreshRequestDto
                {
                    SessionId = credential.SessionId,
                    RefreshToken = credential.RefreshToken
                }, cancellationToken);
            }
        }
        finally
        {
            current = null;
            if (saved is null || sessionId is null || saved.SessionId == sessionId)
            {
                await store.ClearAsync(CancellationToken.None);
            }
        }
    }

    private async Task<AuthenticationTokensDto> RefreshCredentialAsync(
        SavedRefreshSession credential, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await SendAsync("Auth/Refresh", new RefreshRequestDto
            {
                SessionId = credential.SessionId,
                RefreshToken = credential.RefreshToken
            }, cancellationToken);
            var tokens = await ReadTokensAsync(response, cancellationToken);
            if (tokens.SessionId != credential.SessionId)
            {
                throw SessionInvalid();
            }
            await AcceptAsync(tokens, clearSavedLogin: false, cancellationToken);
            return tokens with { RefreshToken = null };
        }
        catch (SessionAuthenticationException exception)
            when (exception.Code == AuthenticationErrorCodes.SessionInvalid)
        {
            current = null;
            var saved = await store.ReadAsync(CancellationToken.None);
            if (saved?.SessionId == credential.SessionId)
            {
                await store.ClearAsync(CancellationToken.None);
            }
            throw;
        }
    }

    private async Task AcceptAsync(
        AuthenticationTokensDto tokens, bool clearSavedLogin, CancellationToken cancellationToken)
    {
        try
        {
            if (tokens.RememberLogin)
            {
                await store.SaveAsync(ToCredential(tokens), cancellationToken);
            }
            else if (clearSavedLogin)
            {
                await store.ClearAsync(cancellationToken);
            }
            current = tokens;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or CryptographicException or OperationCanceledException)
        {
            current = null;
            await TryRevokeAsync(ToCredential(tokens));
            throw new InvalidOperationException("无法安全更新本机登录凭据，请重新登录。", exception);
        }
    }

    private async Task TryRevokeAsync(SavedRefreshSession credential)
    {
        try
        {
            using var response = await SendAsync("Auth/Logout", new RefreshRequestDto
            {
                SessionId = credential.SessionId,
                RefreshToken = credential.RefreshToken
            }, CancellationToken.None);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or HttpRequestException or OperationCanceledException)
        {
            logger.LogWarning(exception, "未能撤销已替换的桌面登录会话。");
        }
    }

    private async Task<HttpResponseMessage> SendAsync(
        string path, object request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await client.PostAsJsonAsync(path, request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException("无法连接认证服务，请检查网络后重试。", exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("认证请求超时，请稍后重试。", exception);
        }

        if (response.IsSuccessStatusCode)
        {
            return response;
        }
        using (response)
        {
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Conflict)
            {
                AuthenticationErrorDto? error = null;
                try
                {
                    error = await response.Content.ReadFromJsonAsync<AuthenticationErrorDto>(cancellationToken);
                }
                catch (JsonException)
                {
                    // 代理返回非协议格式时也不把未知内容展示为认证错误。
                }
                throw new SessionAuthenticationException(
                    error?.Code ?? AuthenticationErrorCodes.SessionInvalid,
                    error?.Message ?? "登录会话已失效，请重新登录。");
            }
            throw new InvalidOperationException($"认证服务暂时不可用（HTTP {(int)response.StatusCode}），请稍后重试。");
        }
    }

    private static async Task<AuthenticationTokensDto> ReadTokensAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var tokens = await response.Content.ReadFromJsonAsync<AuthenticationTokensDto>(cancellationToken);
            if (tokens is null || tokens.SessionId == Guid.Empty ||
                string.IsNullOrEmpty(tokens.AccessToken) || string.IsNullOrEmpty(tokens.RefreshToken) ||
                tokens.RefreshToken.Length > 128 || tokens.AccessTokenExpiresAtUtc <= DateTimeOffset.UtcNow ||
                tokens.RefreshExpiresAtUtc <= DateTimeOffset.UtcNow ||
                tokens.SessionExpiresAtUtc < tokens.RefreshExpiresAtUtc)
            {
                throw new InvalidOperationException("认证服务返回了无效的凭据。");
            }
            var user = new UserInfo(tokens.AccessToken);
            if (user.SessionId != tokens.SessionId || user.ExpiresAt != tokens.AccessTokenExpiresAtUtc)
            {
                throw new InvalidOperationException("认证服务返回的会话与访问令牌不一致。");
            }
            return tokens;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            throw new InvalidOperationException("认证服务返回了无效的凭据格式。", exception);
        }
    }

    private static SavedRefreshSession ToCredential(AuthenticationTokensDto tokens) =>
        new(tokens.SessionId, tokens.RefreshToken!, tokens.SessionExpiresAtUtc);

    private static SessionAuthenticationException SessionInvalid() =>
        new(AuthenticationErrorCodes.SessionInvalid, "登录会话已失效，请重新登录。");
}
