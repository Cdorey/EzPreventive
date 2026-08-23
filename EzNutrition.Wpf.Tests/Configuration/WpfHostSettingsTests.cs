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

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}
