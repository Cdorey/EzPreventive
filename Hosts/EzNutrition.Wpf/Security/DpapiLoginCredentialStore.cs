using EzNutrition.Wpf.Configuration;
using System.Diagnostics;
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
internal sealed class DpapiLoginCredentialStore
{
    private const int CurrentFormatVersion = 2;
    // 保留保护用途标识以识别并清理旧格式；旧密码不会再被反序列化或发送。
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

    /// <summary>获取当前服务连接对应的密文文件路径。</summary>
    internal string CredentialFilePath => credentialFilePath;

    /// <summary>获取当前服务连接是否存在保存的密文文件。</summary>
    internal bool HasSavedCredential => File.Exists(credentialFilePath);

    /// <summary>读取刷新凭据；遇到 2.1 的密码存储格式时删除旧文件并要求重新登录。</summary>
    internal async ValueTask<SavedRefreshSession?> ReadAsync(
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
            if (payload.Version == 1 && string.Equals(payload.Scope, credentialScope, StringComparison.Ordinal))
            {
                await ClearAsync(cancellationToken);
                return null;
            }
            ValidatePayload(payload);
            return new SavedRefreshSession(
                payload.SessionId, payload.RefreshToken!, payload.ExpiresAtUtc);
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

    /// <summary>原子保存刷新凭据的 DPAPI 密文。</summary>
    internal async ValueTask SaveAsync(
        SavedRefreshSession credential,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credential);

        var payload = new CredentialPayload
        {
            Version = CurrentFormatVersion,
            Scope = credentialScope,
            SessionId = credential.SessionId,
            RefreshToken = credential.RefreshToken,
            ExpiresAtUtc = credential.ExpiresAtUtc
        };
        ValidatePayload(payload);
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

    /// <summary>清除当前端点保存的刷新凭据。</summary>
    internal ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        File.Delete(credentialFilePath);
        return ValueTask.CompletedTask;
    }

    /// <summary>用排他文件句柄串行化同一端点的跨进程登录、轮换与退出。</summary>
    internal async Task<FileStream> AcquireLockAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(credentialFilePath)!);
        var started = Stopwatch.GetTimestamp();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(credentialFilePath + ".lock",
                    FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException exception) when ((exception.HResult & 0xffff) is 32 or 33)
            {
                if (Stopwatch.GetElapsedTime(started) >= TimeSpan.FromSeconds(30))
                {
                    throw new InvalidOperationException("其他窗口正在更新登录状态，请稍后重试。", exception);
                }
                await Task.Delay(50, cancellationToken);
            }
        }
    }

    private void ValidatePayload(CredentialPayload payload)
    {
        if (payload.Version != CurrentFormatVersion ||
            !string.Equals(payload.Scope, credentialScope, StringComparison.Ordinal) ||
            payload.SessionId == Guid.Empty ||
            string.IsNullOrEmpty(payload.RefreshToken) || payload.RefreshToken.Length > 128 ||
            payload.ExpiresAtUtc == default)
        {
            throw new InvalidDataException(
                "Windows 登录信息不属于当前连接或使用了不受支持的格式。");
        }

    }

    private sealed class CredentialPayload
    {
        public int Version { get; set; }

        public string? Scope { get; set; }

        public Guid SessionId { get; set; }

        public string? RefreshToken { get; set; }

        public DateTimeOffset ExpiresAtUtc { get; set; }
    }
}

/// <summary>仅包含会话标识、刷新凭据及绝对期限的本机存储格式。</summary>
/// <param name="SessionId">所属会话标识。</param>
/// <param name="RefreshToken">一次性刷新凭据。</param>
/// <param name="ExpiresAtUtc">会话绝对到期时间。</param>
internal sealed record SavedRefreshSession(Guid SessionId, string RefreshToken, DateTimeOffset ExpiresAtUtc);
