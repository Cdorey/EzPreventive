using EzNutrition.Presentation.Services;
using EzNutrition.Wpf.Configuration;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EzNutrition.Wpf.Security;

/// <summary>
/// 使用 Windows DPAPI 在当前用户范围内保护登录信息。
/// </summary>
/// <remarks>
/// 每个服务端地址和传输安全策略使用独立文件，磁盘及临时文件中只出现 DPAPI 密文。
/// 该保护不抵御已经取得当前 Windows 用户执行权限的恶意程序。
/// </remarks>
internal sealed class DpapiLoginCredentialStore : ILoginCredentialStore
{
    private const int CurrentFormatVersion = 1;
    private const int MaximumPasswordCharacters = 4096;
    private const int MaximumUserNameCharacters = 256;
    private static readonly byte[] OptionalEntropy =
        Encoding.UTF8.GetBytes("EzSuit.EzNutrition.Wpf.LoginCredential.v1");
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string credentialFilePath;
    private readonly string credentialScope;

    /// <summary>
    /// 为当前生效的服务连接创建凭据存储。
    /// </summary>
    internal DpapiLoginCredentialStore(
        WpfUserDataPaths paths,
        WpfHostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(settings);

        credentialScope = string.Concat(
            settings.ServerBaseAddress.AbsoluteUri,
            "\n",
            settings.TransportSecurity.ToString());
        var scopeBytes = Encoding.UTF8.GetBytes(credentialScope);
        try
        {
            var fileName = string.Concat(
                Convert.ToHexString(SHA256.HashData(scopeBytes)),
                ".bin");
            credentialFilePath = Path.Combine(paths.CredentialsDirectory, fileName);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(scopeBytes);
        }
    }

    /// <inheritdoc />
    public bool IsAvailable => true;

    /// <summary>获取当前服务连接对应的密文文件路径。</summary>
    internal string CredentialFilePath => credentialFilePath;

    /// <summary>获取当前服务连接是否存在保存的密文文件。</summary>
    internal bool HasSavedCredential => File.Exists(credentialFilePath);

    /// <inheritdoc />
    public async ValueTask<SavedLoginCredential?> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(credentialFilePath))
        {
            return null;
        }

        var protectedContent = await UserDataFileIO.ReadAllBytesAsync(
            credentialFilePath,
            cancellationToken);
        byte[]? clearContent = null;
        try
        {
            clearContent = ProtectedData.Unprotect(
                protectedContent,
                OptionalEntropy,
                DataProtectionScope.CurrentUser);
            var payload = JsonSerializer.Deserialize<CredentialPayload>(
                clearContent,
                SerializerOptions)
                ?? throw new InvalidDataException("Windows 登录信息为空。");
            ValidatePayload(payload);
            return new SavedLoginCredential(payload.UserName!, payload.Password!);
        }
        catch (CryptographicException exception)
        {
            throw new InvalidDataException(
                "Windows 无法解密当前连接保存的登录信息。",
                exception);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "Windows 登录信息的内部格式已损坏。",
                exception);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedContent);
            if (clearContent is not null)
            {
                CryptographicOperations.ZeroMemory(clearContent);
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask SaveAsync(
        SavedLoginCredential credential,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credential);
        ValidateCredentialLength(credential);

        var payload = new CredentialPayload
        {
            Version = CurrentFormatVersion,
            Scope = credentialScope,
            UserName = credential.UserName,
            Password = credential.Password
        };
        var clearContent = JsonSerializer.SerializeToUtf8Bytes(payload, SerializerOptions);
        byte[]? protectedContent = null;
        try
        {
            protectedContent = ProtectedData.Protect(
                clearContent,
                OptionalEntropy,
                DataProtectionScope.CurrentUser);
            await UserDataFileIO.WriteAtomicallyAsync(
                credentialFilePath,
                protectedContent,
                cancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clearContent);
            if (protectedContent is not null)
            {
                CryptographicOperations.ZeroMemory(protectedContent);
            }
        }
    }

    /// <inheritdoc />
    public ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        File.Delete(credentialFilePath);
        return ValueTask.CompletedTask;
    }

    private void ValidatePayload(CredentialPayload payload)
    {
        if (payload.Version != CurrentFormatVersion ||
            !string.Equals(payload.Scope, credentialScope, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(payload.UserName) ||
            string.IsNullOrEmpty(payload.Password))
        {
            throw new InvalidDataException(
                "Windows 登录信息不属于当前连接或使用了不受支持的格式。");
        }

        ValidateCredentialLength(
            new SavedLoginCredential(payload.UserName, payload.Password));
    }

    private static void ValidateCredentialLength(SavedLoginCredential credential)
    {
        if (credential.UserName.Length > MaximumUserNameCharacters ||
            credential.Password.Length > MaximumPasswordCharacters)
        {
            throw new InvalidDataException("登录信息超过本机安全存储允许的长度。");
        }
    }

    private sealed class CredentialPayload
    {
        public int Version { get; set; }

        public string? Scope { get; set; }

        public string? UserName { get; set; }

        public string? Password { get; set; }
    }
}
