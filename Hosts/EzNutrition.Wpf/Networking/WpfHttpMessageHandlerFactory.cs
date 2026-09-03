using EzNutrition.Wpf.Configuration;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace EzNutrition.Wpf.Networking;

/// <summary>
/// 按当前 WPF 连接策略创建彼此独立的主 HTTP 处理器。
/// </summary>
internal sealed class WpfHttpMessageHandlerFactory
{
    private const string ServerAuthenticationOid = "1.3.6.1.5.5.7.3.1";
    private readonly WpfHostSettings settings;

    /// <summary>创建处理器工厂。</summary>
    internal WpfHttpMessageHandlerFactory(WpfHostSettings settings)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>为一个命名客户端创建新的主 HTTP 处理器。</summary>
    internal HttpMessageHandler Create()
    {
        // 不自动跟随 3xx，避免登录表单或其他敏感请求经 307/308 被转发到
        // 配置端点之外；机构应直接配置最终 API 地址。
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false
        };
        if (settings.TransportSecurity == ServerTransportSecurity.AllowSelfSignedHttps)
        {
            handler.ServerCertificateCustomValidationCallback = ValidateServerCertificate;
        }

        return handler;
    }

    private bool ValidateServerCertificate(
        HttpRequestMessage request,
        X509Certificate2? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors)
    {
        if (sslPolicyErrors == SslPolicyErrors.None)
        {
            return true;
        }

        return IsAcceptableSelfSignedCertificate(
            settings.ServerBaseAddress,
            request.RequestUri,
            certificate,
            sslPolicyErrors);
    }

    /// <summary>
    /// 只在请求仍指向配置端点、没有域名错误且证书能以自身作为唯一受信根时接受证书。
    /// </summary>
    internal static bool IsAcceptableSelfSignedCertificate(
        Uri configuredEndpoint,
        Uri? requestUri,
        X509Certificate2? certificate,
        SslPolicyErrors sslPolicyErrors)
    {
        ArgumentNullException.ThrowIfNull(configuredEndpoint);
        if (requestUri is null ||
            certificate is null ||
            sslPolicyErrors != SslPolicyErrors.RemoteCertificateChainErrors ||
            !HasSameAuthority(configuredEndpoint, requestUri) ||
            !certificate.SubjectName.RawData.AsSpan().SequenceEqual(
                certificate.IssuerName.RawData))
        {
            return false;
        }

        using var verificationChain = new X509Chain();
        verificationChain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        verificationChain.ChainPolicy.CustomTrustStore.Add(certificate);
        verificationChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        verificationChain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
        verificationChain.ChainPolicy.DisableCertificateDownloads = true;
        verificationChain.ChainPolicy.ApplicationPolicy.Add(
            new Oid(ServerAuthenticationOid));

        return verificationChain.Build(certificate) &&
            verificationChain.ChainElements.Count == 1 &&
            certificate.RawDataMemory.Span.SequenceEqual(
                verificationChain.ChainElements[0].Certificate.RawDataMemory.Span);
    }

    private static bool HasSameAuthority(Uri configuredEndpoint, Uri requestUri) =>
        string.Equals(
            configuredEndpoint.Scheme,
            requestUri.Scheme,
            StringComparison.OrdinalIgnoreCase) &&
        string.Equals(
            configuredEndpoint.IdnHost,
            requestUri.IdnHost,
            StringComparison.OrdinalIgnoreCase) &&
        configuredEndpoint.Port == requestUri.Port;
}
