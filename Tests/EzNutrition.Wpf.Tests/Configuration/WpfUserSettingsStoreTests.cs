using EzNutrition.Wpf.Configuration;
using Microsoft.Extensions.Configuration;

namespace EzNutrition.Wpf.Tests.Configuration;

/// <summary>
/// 验证 WPF 用户连接设置的边界、持久化格式与损坏降级行为。
/// </summary>
public sealed class WpfUserSettingsStoreTests
{
    [Fact]
    public async Task Saved_connection_settings_can_be_loaded_as_host_overrides()
    {
        using var temporary = new TempDirectory();
        var paths = WpfUserDataPaths.Create(temporary.RootPath);
        var currentSettings = CreateSettings(
            "https://eznutrition.cdorey.net/",
            ServerTransportSecurity.StrictHttps,
            temporary.RootPath);
        var store = new WpfUserSettingsStore(paths, currentSettings);

        await store.SaveAsync(
            new Uri("https://institution.example.test/nutrition"),
            ServerTransportSecurity.AllowSelfSignedHttps);
        var overrides = WpfUserSettingsStore.ReadConfigurationOverrides(
            paths.SettingsFilePath,
            out var warning);

        Assert.Null(warning);
        Assert.Equal(
            "https://institution.example.test/nutrition/",
            overrides["EzNutrition:ServerBaseAddress"]);
        Assert.Equal(
            "AllowSelfSignedHttps",
            overrides["EzNutrition:TransportSecurity"]);
        Assert.Empty(Directory.EnumerateFiles(
            temporary.RootPath,
            "*.tmp",
            SearchOption.AllDirectories));
    }

    [Fact]
    public void Invalid_user_settings_are_ignored_without_deleting_the_source_file()
    {
        using var temporary = new TempDirectory();
        var paths = WpfUserDataPaths.Create(temporary.RootPath);
        File.WriteAllText(
            paths.SettingsFilePath,
            """{"EzNutrition":{"ServerBaseAddress":"http://unsafe.test/","TransportSecurity":"StrictHttps"}}""");

        var overrides = WpfUserSettingsStore.ReadConfigurationOverrides(
            paths.SettingsFilePath,
            out var warning);

        Assert.Empty(overrides);
        Assert.NotNull(warning);
        Assert.True(File.Exists(paths.SettingsFilePath));
    }

    [Fact]
    public void Oversized_user_settings_are_ignored()
    {
        using var temporary = new TempDirectory();
        var paths = WpfUserDataPaths.Create(temporary.RootPath);
        File.WriteAllBytes(
            paths.SettingsFilePath,
            new byte[UserDataFileIO.MaximumFileBytes + 1]);

        var overrides = WpfUserSettingsStore.ReadConfigurationOverrides(
            paths.SettingsFilePath,
            out var warning);

        Assert.Empty(overrides);
        Assert.NotNull(warning);
    }

    internal static WpfHostSettings CreateSettings(
        string serverBaseAddress,
        ServerTransportSecurity transportSecurity,
        string archiveRoot)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EzNutrition:ServerBaseAddress"] = serverBaseAddress,
                ["EzNutrition:UpdateFeedAddress"] = "https://updates.example.test/eznutrition/",
                ["EzNutrition:TransportSecurity"] = transportSecurity.ToString(),
                ["EzNutrition:ArchiveRootPath"] = Path.Combine(archiveRoot, "Archives")
            })
            .Build();
        return WpfHostSettings.Create(configuration);
    }
}
