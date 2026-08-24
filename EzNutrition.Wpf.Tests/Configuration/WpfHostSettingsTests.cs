using EzNutrition.Wpf.Configuration;
using Microsoft.Extensions.Configuration;

namespace EzNutrition.Wpf.Tests.Configuration;

public sealed class WpfHostSettingsTests
{
    [Fact]
    public void Configuration_normalizes_server_address_and_applies_archive_override()
    {
        using var temporary = new TempDirectory();
        var archiveRoot = Path.Combine(temporary.RootPath, "archives");
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["EzNutrition:ServerBaseAddress"] = "https://example.test/api",
            ["EzNutrition:ArchiveRootPath"] = archiveRoot
        });

        var settings = WpfHostSettings.Create(configuration);

        Assert.Equal(new Uri("https://example.test/api/"), settings.ServerBaseAddress);
        Assert.Equal(ServerTransportSecurity.StrictHttps, settings.TransportSecurity);
        Assert.Equal(archiveRoot, settings.ArchiveStorage.RootPath);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("relative/path")]
    [InlineData("ftp://example.test/")]
    public void Missing_or_non_http_server_address_is_rejected(string? serverAddress)
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["EzNutrition:ServerBaseAddress"] = serverAddress
        });

        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = WpfHostSettings.Create(configuration);
        });
    }

    [Theory]
    [InlineData("StrictHttps")]
    [InlineData("AllowSelfSignedHttps")]
    public void Https_security_modes_require_an_https_address(string transportSecurity)
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["EzNutrition:ServerBaseAddress"] = "http://example.test/",
            ["EzNutrition:TransportSecurity"] = transportSecurity
        });

        Assert.Throws<InvalidOperationException>(() => WpfHostSettings.Create(configuration));
    }

    [Fact]
    public void Insecure_http_mode_requires_an_http_address()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["EzNutrition:ServerBaseAddress"] = "https://example.test/",
            ["EzNutrition:TransportSecurity"] = "InsecureHttp"
        });

        Assert.Throws<InvalidOperationException>(() => WpfHostSettings.Create(configuration));
    }

    [Theory]
    [InlineData("https://user:password@example.test/")]
    [InlineData("https://example.test/?tenant=one")]
    [InlineData("https://example.test/#section")]
    public void Embedded_credentials_query_and_fragment_are_rejected(string serverAddress)
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["EzNutrition:ServerBaseAddress"] = serverAddress,
            ["EzNutrition:TransportSecurity"] = "StrictHttps"
        });

        Assert.Throws<InvalidOperationException>(() => WpfHostSettings.Create(configuration));
    }

    [Fact]
    public void Unknown_transport_security_mode_is_rejected()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["EzNutrition:ServerBaseAddress"] = "https://example.test/",
            ["EzNutrition:TransportSecurity"] = "TrustEverything"
        });

        Assert.Throws<InvalidOperationException>(() => WpfHostSettings.Create(configuration));
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}
