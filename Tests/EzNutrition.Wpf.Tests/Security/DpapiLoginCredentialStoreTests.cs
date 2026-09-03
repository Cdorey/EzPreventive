using EzNutrition.Wpf.Configuration;
using EzNutrition.Wpf.Security;
using EzNutrition.Wpf.Tests.Configuration;
using System.Text;
using System.Security.Cryptography;
using System.Text.Json;

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
        var credential = new SavedRefreshSession(
            Guid.NewGuid(), "local-test-refresh-token", DateTimeOffset.UtcNow.AddDays(30));

        await store.SaveAsync(credential);
        var restored = await store.ReadAsync();
        var protectedContent = await File.ReadAllBytesAsync(store.CredentialFilePath);

        Assert.NotNull(restored);
        Assert.Equal(credential, restored);
        Assert.Equal(
            -1,
            protectedContent.AsSpan().IndexOf(
                Encoding.UTF8.GetBytes(credential.SessionId.ToString("D"))));
        Assert.Equal(
            -1,
            protectedContent.AsSpan().IndexOf(
                Encoding.UTF8.GetBytes(credential.RefreshToken)));
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
            new SavedRefreshSession(Guid.NewGuid(), "scoped-refresh-token", DateTimeOffset.UtcNow.AddDays(30)));

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
        await firstStore.SaveAsync(new SavedRefreshSession(Guid.NewGuid(), "first-refresh", DateTimeOffset.UtcNow.AddDays(30)));
        await secondStore.SaveAsync(new SavedRefreshSession(Guid.NewGuid(), "second-refresh", DateTimeOffset.UtcNow.AddDays(30)));

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

    [Fact]
    public async Task Legacy_password_file_is_deleted_instead_of_being_restored()
    {
        using var temporary = new TempDirectory();
        var store = CreateStore(temporary, "https://server.example.test/", ServerTransportSecurity.StrictHttps);
        var legacy = JsonSerializer.SerializeToUtf8Bytes(new
        {
            version = 1,
            scope = "https://server.example.test/\nStrictHttps",
            userName = "legacy-user",
            password = "legacy-password"
        });
        var encrypted = ProtectedData.Protect(legacy,
            Encoding.UTF8.GetBytes("EzSuit.EzNutrition.Wpf.LoginCredential.v1"), DataProtectionScope.CurrentUser);
        Directory.CreateDirectory(Path.GetDirectoryName(store.CredentialFilePath)!);
        await File.WriteAllBytesAsync(store.CredentialFilePath, encrypted);
        Assert.Null(await store.ReadAsync());
        Assert.False(store.HasSavedCredential);
    }

    [Fact]
    public async Task Separate_store_instances_share_an_exclusive_cancellable_lock()
    {
        using var temporary = new TempDirectory();
        var first = CreateStore(temporary, "https://server.example.test/", ServerTransportSecurity.StrictHttps);
        var second = CreateStore(temporary, "https://server.example.test/", ServerTransportSecurity.StrictHttps);
        await using (var held = await first.AcquireLockAsync())
        {
            using var cancellation = new CancellationTokenSource();
            var waiting = second.AcquireLockAsync(cancellation.Token);
            Assert.False(waiting.IsCompleted);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        }
        await using var available = await second.AcquireLockAsync();
        Assert.True(available.CanWrite);
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
