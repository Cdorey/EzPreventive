using System.Text;
using System.Text.Json;

namespace EzNutrition.Shared.Utilities
{
    public static class HttpUtility
    {
        public static async Task<string> GetAuthorizedStringAsync(this HttpClient http, string token, string requestUri)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Add("Authorization", $"Bearer {token}");
            var response = await http.SendAsync(request);
            return await response.Content.ReadAsStringAsync();
        }
        public static async Task<T> GetAuthorizedJsonAsync<T>(this HttpClient http, string token, string requestUri)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Add("Authorization", $"Bearer {token}");
            var response = await http.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new ArgumentNullException(nameof(content), content);
                return result;
            }

            throw new HttpRequestException($"requestUri:{requestUri},statusCode:{response.StatusCode}");

        }

        public static async Task<HttpResponseMessage> PostAuthorizedJsonAsync<T>(this HttpClient http, string token, string requestUri, T value)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
            request.Headers.Add("Authorization", $"Bearer {token}");
            request.Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");
            return await http.SendAsync(request);
        }

    }
}
