using Microsoft.AspNetCore.Components;
using System.Net.Http.Headers;

namespace EzNutrition.Presentation.Services
{
    public sealed class CustomAuthorizationMessageHandler(
        UserSessionService userSession,
        NavigationManager navigation,
        ApplicationServerEndpoint serverEndpoint) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var attachedAccessToken = false;
            string? attachedToken = null;
            if (request.Headers.Authorization is null
                && IsApplicationRequest(request.RequestUri)
                && userSession.TryGetAccessToken(out var token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                attachedAccessToken = true;
                attachedToken = token;
            }

            var response = await base.SendAsync(request, cancellationToken);
            if (attachedAccessToken
                && response.StatusCode == System.Net.HttpStatusCode.Unauthorized
                && userSession.TryGetAccessToken(out var currentToken)
                && string.Equals(attachedToken, currentToken, StringComparison.Ordinal))
            {
                // 访问令牌失效不等同于用户主动退出；保留安全存储的登录信息，
                // 以便下次启动重新向服务端换取短期令牌。
                await userSession.SignOutAsync(
                    forgetSavedLogin: false,
                    cancellationToken);
                navigation.NavigateTo("", replace: true);
            }

            return response;
        }

        private bool IsApplicationRequest(Uri? requestUri) =>
            requestUri is not null
            && (!requestUri.IsAbsoluteUri || serverEndpoint.BaseAddress.IsBaseOf(requestUri));
    }
}
