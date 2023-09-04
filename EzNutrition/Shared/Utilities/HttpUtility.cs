namespace EzNutrition.Shared.Utilities
{
    public static class HttpUtility
    {
        public static async Task<string> GetAuthorizedStringAsync(this HttpClient http, string token, string? requestUri)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Add("Authorization", $"Bearer {token}");
            var response = await http.SendAsync(request);
            return await response.Content.ReadAsStringAsync();
        }
    }
}
