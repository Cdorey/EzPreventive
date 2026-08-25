using EzNutrition.Presentation.Services;
using EzNutrition.Wpf.Configuration;
using EzNutrition.Wpf.Security;
using EzNutrition.Wpf.Tests.Configuration;
using System.Text;

namespace EzNutrition.Wpf.Tests.Security;

/// <summary>
/// 验证 Windows 当前用户登录信息存储的加密、隔离和清理行为。
/// </summary>
public sealed class DpapiLoginCredentialStoreTests
{
    [Fact]
    public async Task Credential_round_trips_without_plaintext_on_disk()
    {
        using var temporary = new TempDirectory();
        var store = CreateStore(
            temporary,
            "https://server.example.test/",
            ServerTransportSecurity.StrictHttps);
        var credential = new SavedLoginCredential(
            "local-test-user",
            "local-test-password");

        await store.SaveAsync(credential);
        var restored = await store.ReadAsync();
        var protectedContent = await File.ReadAllBytesAsync(store.CredentialFilePath);

        Assert.NotNull(restored);
        Assert.Equal(credential.UserName, restored.UserName);
        Assert.Equal(credential.Password, restored.Password);
        Assert.Equal(
            -1,
            protectedContent.AsSpan().IndexOf(
                Encoding.UTF8.GetBytes(credential.UserName)));
        Assert.Equal(
            -1,
            protectedContent.AsSpan().IndexOf(
                Encoding.UTF8.GetBytes(credential.Password)));
        Assert.Empty(Directory.EnumerateFiles(
            temporary.RootPath,
            "*.tmp",
            SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Credentials_are_isolated_by_endpoint_and_security_mode()
    {
        using var temporary = new TempDirectory();
        var strictStore = CreateStore(
            temporary,
            "https://server.example.test/",
            ServerTransportSecurity.StrictHttps);
        var otherEndpointStore = CreateStore(
            temporary,
            "https://other.example.test/",
            ServerTransportSecurity.StrictHttps);
        var selfSignedStore = CreateStore(
            temporary,
            "https://server.example.test/",
            ServerTransportSecurity.AllowSelfSignedHttps);

        await strictStore.SaveAsync(
            new SavedLoginCredential("scoped-user", "scoped-password"));

        Assert.Null(await otherEndpointStore.ReadAsync());
        Assert.Null(await selfSignedStore.ReadAsync());
        Assert.NotEqual(strictStore.CredentialFilePath, otherEndpointStore.CredentialFilePath);
        Assert.NotEqual(strictStore.CredentialFilePath, selfSignedStore.CredentialFilePath);
    }

    [Fact]
    public async Task Clear_removes_only_the_current_scope_file()
    {
        using var temporary = new TempDirectory();
        var firstStore = CreateStore(
            temporary,
            "https://first.example.test/",
            ServerTransportSecurity.StrictHttps);
        var secondStore = CreateStore(
            temporary,
            "https://second.example.test/",
            ServerTransportSecurity.StrictHttps);
        await firstStore.SaveAsync(new SavedLoginCredential("first-user", "first-password"));
        await secondStore.SaveAsync(new SavedLoginCredential("second-user", "second-password"));

        await firstStore.ClearAsync();

        Assert.False(File.Exists(firstStore.CredentialFilePath));
        Assert.True(File.Exists(secondStore.CredentialFilePath));
        Assert.NotNull(await secondStore.ReadAsync());
    }

    [Fact]
    public async Task Corrupted_ciphertext_is_rejected_without_being_deleted()
    {
        using var temporary = new TempDirectory();
        var store = CreateStore(
            temporary,
            "https://server.example.test/",
            ServerTransportSecurity.StrictHttps);
        Directory.CreateDirectory(Path.GetDirectoryName(store.CredentialFilePath)!);
        await File.WriteAllBytesAsync(
            store.CredentialFilePath,
            Encoding.UTF8.GetBytes("not-dpapi-ciphertext"));

        await Assert.ThrowsAsync<InvalidDataException>(
            async () => await store.ReadAsync());

        Assert.True(File.Exists(store.CredentialFilePath));
    }

    private static DpapiLoginCredentialStore CreateStore(
        TempDirectory temporary,
        string serverBaseAddress,
        ServerTransportSecurity transportSecurity)
    {
        var paths = WpfUserDataPaths.Create(temporary.RootPath);
        var settings = WpfUserSettingsStoreTests.CreateSettings(
            serverBaseAddress,
            transportSecurity,
            temporary.RootPath);
        return new DpapiLoginCredentialStore(paths, settings);
    }
}
