using Microsoft.AspNetCore.Components;
using System.Net.Http.Headers;

namespace EzNutrition.Client.Services
{
    public sealed class CustomAuthorizationMessageHandler(
        UserSessionService userSession,
        NavigationManager navigation) : DelegatingHandler
    {
        private readonly Uri baseAddress = new(navigation.BaseUri);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Headers.Authorization is null
                && IsApplicationRequest(request.RequestUri)
                && userSession.TryGetAccessToken(out var token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            return base.SendAsync(request, cancellationToken);
        }

        private bool IsApplicationRequest(Uri? requestUri) =>
            requestUri is not null
            && (!requestUri.IsAbsoluteUri || baseAddress.IsBaseOf(requestUri));
    }
}
