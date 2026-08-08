using EzNutrition.UI.Http;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace EzNutrition.Client.Infrastructure;

/// <summary>
/// 为 Blazor WebAssembly 请求启用浏览器响应流。
/// </summary>
public sealed class BrowserStreamingHttpRequestConfigurator : IStreamingHttpRequestConfigurator
{
    /// <inheritdoc />
    public void EnableResponseStreaming(HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.SetBrowserResponseStreamingEnabled(true);
    }
}
