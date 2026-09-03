using Microsoft.AspNetCore.Components;
using EzNutrition.Shared.Data.DTO;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace EzNutrition.Presentation.Services
{
    /// <summary>为应用请求附加有效访问令牌，并在明确安全的情况下重试一次过期请求。</summary>
    public sealed class CustomAuthorizationMessageHandler(
        UserSessionService userSession,
        NavigationManager navigation,
        ApplicationServerEndpoint serverEndpoint) : DelegatingHandler
    {
        /// <summary>调用方明确允许在服务器尚未执行业务的认证失败后重发请求。</summary>
        public static readonly HttpRequestOptionsKey<bool> AllowAuthenticationRetry =
            new("EzNutrition.AllowAuthenticationRetry");

        /// <inheritdoc />
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Headers.Authorization is not null || !IsApplicationRequest(request.RequestUri))
            {
                return await base.SendAsync(request, cancellationToken);
            }

            string? token;
            try
            {
                token = await userSession.GetValidAccessTokenAsync(cancellationToken);
            }
            catch (InvalidOperationException exception)
            {
                return CreateFailure(request, exception);
            }
            if (token is null)
            {
                return await base.SendAsync(request, cancellationToken);
            }

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await base.SendAsync(request, cancellationToken);
            if (response.StatusCode != HttpStatusCode.Unauthorized)
            {
                return response;
            }

            var error = await ReadAuthenticationErrorAsync(response, cancellationToken);
            if (error?.Code == AuthenticationErrorCodes.SessionInvalid)
            {
                await userSession.RejectAccessTokenAsync(token, cancellationToken);
                if (userSession.UserInfo is null)
                {
                    navigation.NavigateTo("", replace: true);
                }
                return response;
            }
            if (error?.Code != AuthenticationErrorCodes.AccessTokenExpired || !CanRetry(request))
            {
                return response;
            }

            try
            {
                var replacement = await userSession.GetValidAccessTokenAsync(cancellationToken, token);
                if (replacement is null)
                {
                    return response;
                }

                using var retry = await CloneRequestAsync(request, cancellationToken);
                retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", replacement);
                response.Dispose();
                // 直接调用内部处理器，避免第二次 401 进入递归刷新。
                var retriedResponse = await base.SendAsync(retry, cancellationToken);
                if (retriedResponse.StatusCode == HttpStatusCode.Unauthorized)
                {
                    var retryError = await ReadAuthenticationErrorAsync(retriedResponse, cancellationToken);
                    if (retryError?.Code == AuthenticationErrorCodes.SessionInvalid)
                    {
                        await userSession.RejectAccessTokenAsync(replacement, cancellationToken);
                    }
                }
                return retriedResponse;
            }
            catch (InvalidOperationException exception)
            {
                response.Dispose();
                return CreateFailure(request, exception);
            }
        }

        private static bool CanRetry(HttpRequestMessage request) =>
            (request.Content is null || request.Content is ByteArrayContent) &&
            (request.Method == HttpMethod.Get || request.Method == HttpMethod.Head ||
                (request.Options.TryGetValue(AllowAuthenticationRetry, out var allowed) && allowed));

        private static async Task<HttpRequestMessage> CloneRequestAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var clone = new HttpRequestMessage(request.Method, request.RequestUri)
            {
                Version = request.Version,
                VersionPolicy = request.VersionPolicy
            };
            foreach (var header in request.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            foreach (var option in request.Options)
            {
                clone.Options.Set(new HttpRequestOptionsKey<object?>(option.Key), option.Value);
            }
            if (request.Content is not null)
            {
                clone.Content = new ByteArrayContent(await request.Content.ReadAsByteArrayAsync(cancellationToken));
                foreach (var header in request.Content.Headers)
                {
                    clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }
            return clone;
        }

        private static async Task<AuthenticationErrorDto?> ReadAuthenticationErrorAsync(
            HttpResponseMessage response, CancellationToken cancellationToken)
        {
            if (response.Content.Headers.ContentType?.MediaType != "application/json")
            {
                return null;
            }
            try
            {
                return await response.Content.ReadFromJsonAsync<AuthenticationErrorDto>(cancellationToken);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static HttpResponseMessage CreateFailure(HttpRequestMessage request, InvalidOperationException exception) =>
            new(exception is SessionAuthenticationException ? HttpStatusCode.Unauthorized : HttpStatusCode.ServiceUnavailable)
            {
                RequestMessage = request,
                Content = JsonContent.Create(new AuthenticationErrorDto(
                    exception is SessionAuthenticationException authentication
                        ? authentication.Code : "temporarily_unavailable",
                    exception.Message))
            };

        private bool IsApplicationRequest(Uri? requestUri) =>
            requestUri is not null
            && (!requestUri.IsAbsoluteUri || serverEndpoint.BaseAddress.IsBaseOf(requestUri));
    }
}
