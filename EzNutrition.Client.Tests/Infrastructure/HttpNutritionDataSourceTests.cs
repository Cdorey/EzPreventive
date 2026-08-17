using System.Net;
using System.Text;
using EzNutrition.Application.Ports;
using EzNutrition.Client.Infrastructure;

namespace EzNutrition.Client.Tests.Infrastructure;

public sealed class HttpNutritionDataSourceTests
{
    [Fact]
    public async Task Half_year_age_is_sent_as_invariant_decimal_route_value()
    {
        var handler = new CapturingHandler();
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.invalid/")
        };
        var source = new HttpNutritionDataSource(new StubHttpClientFactory(client));

        await source.GetDietaryReferenceIntakesAsync(new NutritionSubjectQuery
        {
            Gender = "女",
            AgeInYears = 0.5m
        });

        Assert.Equal(
            "https://example.invalid/Energy/DRIs/%E5%A5%B3/0.5",
            handler.RequestUri?.AbsoluteUri);
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            });
        }
    }
}
