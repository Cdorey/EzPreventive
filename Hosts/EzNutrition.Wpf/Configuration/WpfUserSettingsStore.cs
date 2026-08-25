using System.Text.Json;

namespace EzNutrition.Wpf.Configuration;

/// <summary>
/// 读取和保存由 WPF 设置窗口管理的当前用户连接设置。
/// </summary>
internal sealed class WpfUserSettingsStore
{
    private const string ServerBaseAddressKey = "EzNutrition:ServerBaseAddress";
    private const string TransportSecurityKey = "EzNutrition:TransportSecurity";
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string filePath;

    /// <summary>创建用户设置存储。</summary>
    internal WpfUserSettingsStore(
        WpfUserDataPaths paths,
        WpfHostSettings currentSettings)
    {
        ArgumentNullException.ThrowIfNull(paths);
        CurrentSettings = currentSettings ??
            throw new ArgumentNullException(nameof(currentSettings));
        ConfiguredServerBaseAddress = currentSettings.ServerBaseAddress;
        ConfiguredTransportSecurity = currentSettings.TransportSecurity;
        filePath = paths.SettingsFilePath;
    }

    /// <summary>获取本次进程启动时已经生效的设置。</summary>
    internal WpfHostSettings CurrentSettings { get; }

    /// <summary>获取设置窗口最近读取或保存的服务端地址。</summary>
    internal Uri ConfiguredServerBaseAddress { get; private set; }

    /// <summary>获取设置窗口最近读取或保存的传输安全策略。</summary>
    internal ServerTransportSecurity ConfiguredTransportSecurity { get; private set; }

    /// <summary>获取用户设置文件路径。</summary>
    internal string FilePath => filePath;

    /// <summary>
    /// 从设置文件读取可追加到宿主配置的键值；损坏或不安全的文件不会参与启动。
    /// </summary>
    internal static IReadOnlyDictionary<string, string?> ReadConfigurationOverrides(
        string filePath,
        out string? warning)
    {
        warning = null;
        if (!File.Exists(filePath))
        {
            return new Dictionary<string, string?>();
        }

        try
        {
            var content = UserDataFileIO.ReadAllBytes(filePath);
            var document = JsonSerializer.Deserialize<UserSettingsDocument>(
                content,
                SerializerOptions)
                ?? throw new InvalidDataException("用户设置文档为空。");
            var serverSettings = document.EzNutrition
                ?? throw new InvalidDataException("用户设置缺少 EzNutrition 节。");
            if (!Enum.TryParse<ServerTransportSecurity>(
                    serverSettings.TransportSecurity,
                    ignoreCase: true,
                    out var transportSecurity) ||
                !Enum.IsDefined(transportSecurity))
            {
                throw new InvalidDataException("用户设置包含未知的传输安全策略。");
            }

            var serverAddress = WpfHostSettings.ValidateServerConnection(
                serverSettings.ServerBaseAddress,
                transportSecurity);
            return new Dictionary<string, string?>
            {
                [ServerBaseAddressKey] = serverAddress.AbsoluteUri,
                [TransportSecurityKey] = transportSecurity.ToString()
            };
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or
            JsonException or InvalidDataException or InvalidOperationException)
        {
            warning = $"忽略无法读取或校验失败的用户连接设置：{exception.Message}";
            return new Dictionary<string, string?>();
        }
    }

    /// <summary>
    /// 原子保存连接设置；新设置会在下次启动时生效。
    /// </summary>
    internal async ValueTask SaveAsync(
        Uri serverBaseAddress,
        ServerTransportSecurity transportSecurity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serverBaseAddress);
        var normalizedAddress = WpfHostSettings.ValidateServerConnection(
            serverBaseAddress.AbsoluteUri,
            transportSecurity);
        var document = new UserSettingsDocument
        {
            EzNutrition = new ServerSettingsDocument
            {
                ServerBaseAddress = normalizedAddress.AbsoluteUri,
                TransportSecurity = transportSecurity.ToString()
            }
        };
        var content = JsonSerializer.SerializeToUtf8Bytes(document, SerializerOptions);
        await UserDataFileIO.WriteAtomicallyAsync(filePath, content, cancellationToken);
        ConfiguredServerBaseAddress = normalizedAddress;
        ConfiguredTransportSecurity = transportSecurity;
    }

    private sealed class UserSettingsDocument
    {
        public ServerSettingsDocument? EzNutrition { get; set; }
    }

    private sealed class ServerSettingsDocument
    {
        public string? ServerBaseAddress { get; set; }

        public string? TransportSecurity { get; set; }
    }
}
