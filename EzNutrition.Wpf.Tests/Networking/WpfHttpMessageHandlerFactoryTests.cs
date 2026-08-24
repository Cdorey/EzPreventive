using EzNutrition.Wpf.Configuration;
using EzNutrition.Wpf.Networking;
using EzNutrition.Wpf.Tests.Configuration;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace EzNutrition.Wpf.Tests.Networking;

/// <summary>
/// 验证桌面宿主不会把“允许自签名”退化为接受任意证书错误。
/// </summary>
public sealed class WpfHttpMessageHandlerFactoryTests
{
    private static readonly Uri ConfiguredEndpoint =
        new("https://self-signed.example.test/");

    [Fact]
    public void Only_self_signed_mode_installs_a_custom_validation_callback()
    {
        using var temporary = new TempDirectory();
        using var strictHandler = Assert.IsType<HttpClientHandler>(
            CreateFactory(
                temporary,
                ServerTransportSecurity.StrictHttps,
                "https://strict.example.test/").Create());
        using var selfSignedHandler = Assert.IsType<HttpClientHandler>(
            CreateFactory(
                temporary,
                ServerTransportSecurity.AllowSelfSignedHttps,
                ConfiguredEndpoint.AbsoluteUri).Create());
        using var httpHandler = Assert.IsType<HttpClientHandler>(
            CreateFactory(
                temporary,
                ServerTransportSecurity.InsecureHttp,
                "http://isolated.example.test/").Create());

        Assert.Null(strictHandler.ServerCertificateCustomValidationCallback);
        Assert.NotNull(selfSignedHandler.ServerCertificateCustomValidationCallback);
        Assert.Null(httpHandler.ServerCertificateCustomValidationCallback);
        Assert.False(strictHandler.AllowAutoRedirect);
        Assert.False(selfSignedHandler.AllowAutoRedirect);
        Assert.False(httpHandler.AllowAutoRedirect);
    }

    [Fact]
    public void Valid_self_signed_server_certificate_is_accepted_for_the_configured_authority()
    {
        using var certificate = CreateSelfSignedServerCertificate(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddDays(2));

        var accepted = WpfHttpMessageHandlerFactory.IsAcceptableSelfSignedCertificate(
            ConfiguredEndpoint,
            ConfiguredEndpoint,
            certificate,
            SslPolicyErrors.RemoteCertificateChainErrors);

        Assert.True(accepted);
    }

    [Theory]
    [InlineData(SslPolicyErrors.RemoteCertificateNameMismatch)]
    [InlineData(SslPolicyErrors.RemoteCertificateNotAvailable)]
    [InlineData(SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateChainErrors)]
    public void Name_missing_and_combined_certificate_errors_are_rejected(
        SslPolicyErrors sslPolicyErrors)
    {
        using var certificate = CreateSelfSignedServerCertificate(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddDays(2));

        var accepted = WpfHttpMessageHandlerFactory.IsAcceptableSelfSignedCertificate(
            ConfiguredEndpoint,
            ConfiguredEndpoint,
            certificate,
            sslPolicyErrors);

        Assert.False(accepted);
    }

    [Fact]
    public void Self_signed_exception_does_not_follow_redirects_to_another_authority()
    {
        using var certificate = CreateSelfSignedServerCertificate(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddDays(2));

        var accepted = WpfHttpMessageHandlerFactory.IsAcceptableSelfSignedCertificate(
            ConfiguredEndpoint,
            new Uri("https://redirected.example.test/"),
            certificate,
            SslPolicyErrors.RemoteCertificateChainErrors);

        Assert.False(accepted);
    }

    [Fact]
    public void Expired_self_signed_certificate_is_rejected()
    {
        using var certificate = CreateSelfSignedServerCertificate(
            DateTimeOffset.UtcNow.AddDays(-3),
            DateTimeOffset.UtcNow.AddDays(-1));

        var accepted = WpfHttpMessageHandlerFactory.IsAcceptableSelfSignedCertificate(
            ConfiguredEndpoint,
            ConfiguredEndpoint,
            certificate,
            SslPolicyErrors.RemoteCertificateChainErrors);

        Assert.False(accepted);
    }

    [Fact]
    public void Certificate_issued_by_an_untrusted_private_ca_is_not_treated_as_self_signed()
    {
        using var privateCa = CreateCertificateAuthority();
        using var certificate = CreateIssuedServerCertificate(privateCa);

        var accepted = WpfHttpMessageHandlerFactory.IsAcceptableSelfSignedCertificate(
            ConfiguredEndpoint,
            ConfiguredEndpoint,
            certificate,
            SslPolicyErrors.RemoteCertificateChainErrors);

        Assert.False(accepted);
    }

    private static WpfHttpMessageHandlerFactory CreateFactory(
        TempDirectory temporary,
        ServerTransportSecurity transportSecurity,
        string serverBaseAddress)
    {
        var settings = WpfUserSettingsStoreTests.CreateSettings(
            serverBaseAddress,
            transportSecurity,
            temporary.RootPath);
        return new WpfHttpMessageHandlerFactory(settings);
    }

    private static X509Certificate2 CreateSelfSignedServerCertificate(
        DateTimeOffset notBefore,
        DateTimeOffset notAfter)
    {
        using var key = RSA.Create(2048);
        var request = CreateServerCertificateRequest(
            "CN=self-signed.example.test",
            key);
        return request.CreateSelfSigned(notBefore, notAfter);
    }

    private static X509Certificate2 CreateCertificateAuthority()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=EzNutrition Test Private CA",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(
                certificateAuthority: true,
                hasPathLengthConstraint: false,
                pathLengthConstraint: 0,
                critical: true));
        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false));
        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(2));
    }

    private static X509Certificate2 CreateIssuedServerCertificate(X509Certificate2 issuer)
    {
        using var key = RSA.Create(2048);
        var request = CreateServerCertificateRequest(
            "CN=self-signed.example.test",
            key);
        var serialNumber = RandomNumberGenerator.GetBytes(16);
        return request.Create(
            issuer,
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddDays(1),
            serialNumber);
    }

    private static CertificateRequest CreateServerCertificateRequest(
        string subjectName,
        RSA key)
    {
        var request = new CertificateRequest(
            subjectName,
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(
                certificateAuthority: false,
                hasPathLengthConstraint: false,
                pathLengthConstraint: 0,
                critical: true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature |
                X509KeyUsageFlags.KeyEncipherment,
                critical: true));
        var enhancedKeyUsage = new OidCollection
        {
            new Oid("1.3.6.1.5.5.7.3.1")
        };
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(enhancedKeyUsage, critical: true));
        var alternativeNames = new SubjectAlternativeNameBuilder();
        alternativeNames.AddDnsName("self-signed.example.test");
        request.CertificateExtensions.Add(alternativeNames.Build());
        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false));
        return request;
    }
}
