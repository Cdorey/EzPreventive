namespace EzNutrition.Wpf.Configuration;

/// <summary>
/// 指定桌面宿主连接服务端时采用的传输安全策略。
/// </summary>
internal enum ServerTransportSecurity
{
    /// <summary>只允许由 Windows 正常验证的 HTTPS 连接。</summary>
    StrictHttps,

    /// <summary>允许当前端点使用经过窄化校验的自签名 HTTPS 证书。</summary>
    AllowSelfSignedHttps,

    /// <summary>允许不提供传输加密的 HTTP 连接。</summary>
    InsecureHttp
}
