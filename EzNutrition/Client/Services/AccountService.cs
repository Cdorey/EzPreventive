using EzNutrition.Shared.Data.DTO;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace EzNutrition.Client.Services;

public sealed class AccountService(
    IHttpClientFactory httpClientFactory,
    ILogger<AccountService> logger)
{
    public Task<AccountOperationResultDto> ResendEmailConfirmationAsync(
        string email,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            "Anonymous",
            HttpMethod.Post,
            "Auth/ResendEmailConfirmation",
            new ResendEmailConfirmationDto { Email = email },
            cancellationToken);

    public Task<AccountOperationResultDto> RequestPasswordResetAsync(
        string email,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            "Anonymous",
            HttpMethod.Post,
            "Auth/ForgotPassword",
            new ForgotPasswordDto { Email = email },
            cancellationToken);

    public Task<AccountOperationResultDto> ResetPasswordAsync(
        ResetPasswordDto request,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            "Anonymous",
            HttpMethod.Post,
            "Auth/ResetPassword",
            request,
            cancellationToken);

    public Task<AccountOperationResultDto> ConfirmEmailAsync(
        ConfirmEmailDto request,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            "Anonymous",
            HttpMethod.Post,
            "Auth/ConfirmEmail",
            request,
            cancellationToken);

    public Task<AccountOperationResultDto> ConfirmEmailChangeAsync(
        ConfirmEmailChangeDto request,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            "Anonymous",
            HttpMethod.Post,
            "Auth/ConfirmEmailChange",
            request,
            cancellationToken);

    public Task<AccountOperationResultDto> ChangePasswordAsync(
        ChangePasswordDto request,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            "Authorize",
            HttpMethod.Post,
            "User/ChangePassword",
            request,
            cancellationToken);

    public Task<AccountOperationResultDto> RequestEmailChangeAsync(
        RequestEmailChangeDto request,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            "Authorize",
            HttpMethod.Post,
            "User/RequestEmailChange",
            request,
            cancellationToken);

    public Task<AccountOperationResultDto> ChangePhoneNumberAsync(
        ChangePhoneNumberDto request,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            "Authorize",
            HttpMethod.Put,
            "User/PhoneNumber",
            request,
            cancellationToken);

    public async Task<UserInfoDto> GetProfileAsync(CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("Authorize");
        try
        {
            return await client.GetFromJsonAsync<UserInfoDto>("User/Profile", cancellationToken)
                ?? throw new InvalidOperationException("服务器没有返回有效的用户资料。");
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("账户请求超时，请稍后重试。", ex);
        }
    }

    private async Task<AccountOperationResultDto> SendAsync<TRequest>(
        string clientName,
        HttpMethod method,
        string requestUri,
        TRequest payload,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(clientName);
        using var request = new HttpRequestMessage(method, requestUri)
        {
            Content = JsonContent.Create(payload)
        };
        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, cancellationToken);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("账户请求超时，请稍后重试。", ex);
        }

        using (response)
        {

            AccountOperationResultDto? result = null;
            try
            {
                result = await response.Content.ReadFromJsonAsync<AccountOperationResultDto>(
                    cancellationToken);
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
                logger.LogWarning(
                    ex,
                    "Account endpoint {RequestUri} returned an unexpected response body.",
                    requestUri);
            }

            if (response.IsSuccessStatusCode && result?.Success is true)
            {
                return result;
            }

            throw new InvalidOperationException(
                result?.Message ?? GetFallbackMessage(response.StatusCode));
        }
    }

    private static string GetFallbackMessage(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.BadRequest => "提交的信息无效，请检查后重试。",
        HttpStatusCode.Unauthorized => "登录状态已失效，请重新登录。",
        HttpStatusCode.Conflict => "该操作与当前账户状态冲突，请刷新后重试。",
        HttpStatusCode.TooManyRequests => "请求过于频繁，请稍后再试。",
        _ when (int)statusCode >= 500 => "账户服务暂时不可用，请稍后重试。",
        _ => "账户操作失败，请稍后重试。"
    };
}
