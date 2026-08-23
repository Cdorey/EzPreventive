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

    /// <summary>获取本机档案存储目录。</summary>
    public required ArchiveStorageDirectory ArchiveStorage { get; init; }

    /// <summary>
    /// 从宿主配置创建并验证设置。
    /// </summary>
    public static WpfHostSettings Create(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(SectionName);
        var rawServerAddress = section["ServerBaseAddress"];
        if (!Uri.TryCreate(rawServerAddress, UriKind.Absolute, out var serverAddress) ||
            (!string.Equals(serverAddress.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(serverAddress.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"配置项 {SectionName}:ServerBaseAddress 必须是绝对 HTTP 或 HTTPS 地址。");
        }

        var normalizedServerAddress = serverAddress.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? serverAddress
            : new Uri(serverAddress.AbsoluteUri + '/', UriKind.Absolute);

        return new WpfHostSettings
        {
            ServerBaseAddress = normalizedServerAddress,
            ArchiveStorage = ArchiveStorageDirectory.Create(section["ArchiveRootPath"])
        };
    }
}
