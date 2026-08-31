using EzNutrition.Wpf.Archives;
using Microsoft.Extensions.Configuration;

namespace EzNutrition.Wpf.Configuration;

/// <summary>
/// 表示启动时完成校验的 WPF 宿主设置。
/// </summary>
internal sealed record WpfHostSettings
{
    private const string SectionName = "EzNutrition";

    /// <summary>获取桌面宿主连接的服务端基地址。</summary>
    public required Uri ServerBaseAddress { get; init; }

    /// <summary>获取 Velopack 直发版本使用的更新源地址。</summary>
    public required Uri UpdateFeedAddress { get; init; }

    /// <summary>获取服务端连接采用的传输安全策略。</summary>
    public required ServerTransportSecurity TransportSecurity { get; init; }

    /// <summary>获取本机档案存储目录。</summary>
    public required ArchiveStorageDirectory ArchiveStorage { get; init; }

    /// <summary>
    /// 从宿主配置创建并验证设置。
    /// </summary>
    public static WpfHostSettings Create(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(SectionName);
        var rawTransportSecurity = section["TransportSecurity"];
        if (!Enum.TryParse<ServerTransportSecurity>(
                rawTransportSecurity ?? nameof(ServerTransportSecurity.StrictHttps),
                ignoreCase: true,
                out var transportSecurity) ||
            !Enum.IsDefined(transportSecurity))
        {
            throw new InvalidOperationException(
                $"配置项 {SectionName}:TransportSecurity 不是受支持的传输安全策略。");
        }

        var normalizedServerAddress = ValidateServerConnection(
            section["ServerBaseAddress"],
            transportSecurity);

        return new WpfHostSettings
        {
            ServerBaseAddress = normalizedServerAddress,
            UpdateFeedAddress = ValidateUpdateFeedAddress(section["UpdateFeedAddress"]),
            TransportSecurity = transportSecurity,
            ArchiveStorage = ArchiveStorageDirectory.Create(section["ArchiveRootPath"])
        };
    }

    /// <summary>
    /// 校验更新源地址，并返回以斜杠结尾的规范 HTTPS 地址。
    /// </summary>
    internal static Uri ValidateUpdateFeedAddress(string? rawUpdateFeedAddress)
    {
        var candidate = rawUpdateFeedAddress?.Trim();
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var updateFeedAddress) ||
            !string.Equals(
                updateFeedAddress.Scheme,
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"配置项 {SectionName}:UpdateFeedAddress 必须是绝对 HTTPS 地址。");
        }

        if (!string.IsNullOrEmpty(updateFeedAddress.UserInfo) ||
            !string.IsNullOrEmpty(updateFeedAddress.Query) ||
            !string.IsNullOrEmpty(updateFeedAddress.Fragment))
        {
            throw new InvalidOperationException(
                "更新源地址不能包含用户名、密码、查询参数或片段。");
        }

        return updateFeedAddress.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? updateFeedAddress
            : new Uri(updateFeedAddress.AbsoluteUri + '/', UriKind.Absolute);
    }

    /// <summary>
    /// 校验服务端地址与传输安全策略的组合，并返回以斜杠结尾的规范地址。
    /// </summary>
    internal static Uri ValidateServerConnection(
        string? rawServerAddress,
        ServerTransportSecurity transportSecurity)
    {
        if (!Enum.IsDefined(transportSecurity))
        {
            throw new InvalidOperationException("遇到不受支持的传输安全策略。");
        }

        var candidate = rawServerAddress?.Trim();
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var serverAddress) ||
            (!string.Equals(serverAddress.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(serverAddress.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"配置项 {SectionName}:ServerBaseAddress 必须是绝对 HTTP 或 HTTPS 地址。");
        }

        if (!string.IsNullOrEmpty(serverAddress.UserInfo) ||
            !string.IsNullOrEmpty(serverAddress.Query) ||
            !string.IsNullOrEmpty(serverAddress.Fragment))
        {
            throw new InvalidOperationException(
                "服务端地址不能包含用户名、密码、查询参数或片段。");
        }

        var requiresHttps = transportSecurity is
            ServerTransportSecurity.StrictHttps or
            ServerTransportSecurity.AllowSelfSignedHttps;
        if (requiresHttps &&
            !string.Equals(serverAddress.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("当前安全策略要求使用 HTTPS 服务端地址。");
        }

        if (transportSecurity == ServerTransportSecurity.InsecureHttp &&
            !string.Equals(serverAddress.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("不加密 HTTP 模式要求使用 http:// 服务端地址。");
        }

        return serverAddress.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? serverAddress
            : new Uri(serverAddress.AbsoluteUri + '/', UriKind.Absolute);
    }
}
