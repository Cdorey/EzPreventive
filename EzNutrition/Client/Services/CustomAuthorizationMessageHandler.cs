using Microsoft.AspNetCore.Components;
using System.Net.Http.Headers;

namespace EzNutrition.Client.Services
{
    public sealed class CustomAuthorizationMessageHandler(
        UserSessionService userSession,
        NavigationManager navigation) : DelegatingHandler
    {
        private readonly Uri baseAddress = new(navigation.BaseUri);

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
                await userSession.SignOutAsync();
                navigation.NavigateTo("", replace: true);
            }

            return response;
        }

        private bool IsApplicationRequest(Uri? requestUri) =>
            requestUri is not null
            && (!requestUri.IsAbsoluteUri || baseAddress.IsBaseOf(requestUri));
    }
}
