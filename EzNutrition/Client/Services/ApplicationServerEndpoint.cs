namespace EzNutrition.Client.Services;

/// <summary>
/// 表示当前宿主连接的 EzNutrition 服务端地址。
/// </summary>
/// <remarks>
/// 浏览器宿主通常与服务端同源，而桌面 Hybrid 宿主的页面地址由 WebView 提供，
/// 因此不能用 <c>NavigationManager.BaseUri</c> 推断 API 的安全边界。
/// </remarks>
public sealed class ApplicationServerEndpoint
{
    /// <summary>
    /// 使用服务端基地址创建端点描述。
    /// </summary>
    /// <param name="baseAddress">以 HTTP 或 HTTPS 表示的绝对服务端基地址。</param>
    public ApplicationServerEndpoint(Uri baseAddress)
    {
        ArgumentNullException.ThrowIfNull(baseAddress);
        if (!baseAddress.IsAbsoluteUri ||
            (!string.Equals(baseAddress.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(baseAddress.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("服务端基地址必须是绝对 HTTP 或 HTTPS URI。", nameof(baseAddress));
        }

        if (!string.IsNullOrEmpty(baseAddress.Query) || !string.IsNullOrEmpty(baseAddress.Fragment))
        {
            throw new ArgumentException("服务端基地址不能包含查询参数或片段。", nameof(baseAddress));
        }

        BaseAddress = baseAddress.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? baseAddress
            : new Uri(baseAddress.AbsoluteUri + '/', UriKind.Absolute);
    }

    /// <summary>获取规范化后、以斜杠结尾的服务端基地址。</summary>
    public Uri BaseAddress { get; }
}
