using EzNutrition.Shared.Data.DTO;
using EzNutrition.Shared.Data.Entities;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;

namespace EzNutrition.Presentation.Services;

/// <summary>独立加载公共部署信息、公告和协议，不参与用户认证或令牌刷新。</summary>
public sealed class PublicSystemInfoService
{
    private readonly HttpClient client;
    private readonly ILogger<PublicSystemInfoService> logger;
    private readonly object initializationLock = new();
    private Task? initializationTask;

    /// <summary>创建公共系统信息服务。</summary>
    /// <param name="httpClientFactory">用于创建匿名系统信息客户端的工厂。</param>
    /// <param name="logger">用于记录公共内容加载故障。</param>
    /// <param name="clientVersion">测试或特殊宿主可显式提供的前端产品版本。</param>
    public PublicSystemInfoService(
        IHttpClientFactory httpClientFactory,
        ILogger<PublicSystemInfoService> logger,
        string? clientVersion = null)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        client = httpClientFactory.CreateClient("Anonymous");
        ClientVersion = NormalizeOptional(clientVersion);
        if (ClientVersion.Length == 0)
        {
            var version = Assembly.GetEntryAssembly()?.GetName().Version
                ?? typeof(PublicSystemInfoService).Assembly.GetName().Version;
            ClientVersion = version?.ToString(4) ?? string.Empty;
        }
    }

    /// <summary>公共内容加载完成后通知使用这些内容的页面。</summary>
    public event Action? StateChanged;

    /// <summary>获取备案编号。</summary>
    public string CaseNumber { get; private set; } = string.Empty;

    /// <summary>获取当前运行的前端宿主产品发行版本。</summary>
    public string ClientVersion { get; }

    /// <summary>获取当前连接的服务端产品发行版本。</summary>
    public string ServerVersion { get; private set; } = string.Empty;

    /// <summary>获取前后端产品代际或接口契约代际是否不一致。</summary>
    public bool HasVersionCompatibilityWarning => IsCompatibilityMismatch(ClientVersion, ServerVersion);

    /// <summary>获取产品说明。</summary>
    public string CoverLetter { get; private set; } = string.Empty;

    /// <summary>获取工作提示。</summary>
    public string Notice { get; private set; } = string.Empty;

    /// <summary>获取服务端当前发布的用户许可协议。</summary>
    public string UserAgreement { get; private set; } = string.Empty;

    /// <summary>获取服务端当前发布的隐私条款。</summary>
    public string PrivacyPolicy { get; private set; } = string.Empty;

    /// <summary>获取首次加载是否已经结束；未发布或加载失败的内容为空。</summary>
    public bool IsLoaded { get; private set; }

    /// <summary>首次加载公共系统信息；并发调用和页面切换共享同一加载任务。</summary>
    /// <param name="cancellationToken">仅取消本次等待，不中断其他页面共用的加载。</param>
    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Task currentInitialization;
        lock (initializationLock)
        {
            currentInitialization = initializationTask ??= LoadAsync();
        }

        return cancellationToken.CanBeCanceled
            ? currentInitialization.WaitAsync(cancellationToken)
            : currentInitialization;
    }

    /// <summary>并行加载各类公共内容，完成后一次性通知界面。</summary>
    private async Task LoadAsync()
    {
        try
        {
            var publicInfoTask = TryGetPublicSystemInfoAsync();
            var coverLetterTask = TryGetNoticeAsync("SystemInfo/CoverLetter/");
            var noticeTask = TryGetNoticeAsync("SystemInfo/Notice/");
            var userAgreementTask = TryGetNoticeAsync("SystemInfo/UserAgreement/");
            var privacyPolicyTask = TryGetNoticeAsync("SystemInfo/PrivacyPolicy/");

            await Task.WhenAll(publicInfoTask, coverLetterTask, noticeTask, userAgreementTask, privacyPolicyTask);

            var publicInfo = await publicInfoTask;
            CaseNumber = NormalizeOptional(publicInfo.CaseNumber);
            ServerVersion = NormalizeOptional(publicInfo.ServerVersion);
            CoverLetter = await coverLetterTask;
            Notice = await noticeTask;
            UserAgreement = await userAgreementTask;
            PrivacyPolicy = await privacyPolicyTask;
        }
        finally
        {
            IsLoaded = true;
            StateChanged?.Invoke();
        }
    }

    /// <summary>仅比较有效版本号的产品代际和接口契约代际。</summary>
    internal static bool IsCompatibilityMismatch(string? clientVersion, string? serverVersion)
    {
        if (!Version.TryParse(clientVersion, out var parsedClientVersion) ||
            !Version.TryParse(serverVersion, out var parsedServerVersion))
        {
            return false;
        }

        return parsedClientVersion.Major != parsedServerVersion.Major ||
            parsedClientVersion.Minor != parsedServerVersion.Minor;
    }

    /// <summary>读取部署信息；网络、超时或响应格式错误均按未取得处理。</summary>
    private async Task<PublicSystemInfoDto> TryGetPublicSystemInfoAsync()
    {
        try
        {
            return await client.GetFromJsonAsync<PublicSystemInfoDto>("SystemInfo/PublicInfo/")
                ?? new PublicSystemInfoDto(null, null);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or NotSupportedException or JsonException or OperationCanceledException)
        {
            logger.LogWarning(exception, "未能加载公共部署信息。");
            return new PublicSystemInfoDto(null, null);
        }
    }

    /// <summary>读取公告或协议；单项失败不会阻止其他公共内容加载。</summary>
    private async Task<string> TryGetNoticeAsync(string requestUri)
    {
        try
        {
            var notice = await client.GetFromJsonAsync<Notice>(requestUri);
            return notice?.Description ?? string.Empty;
        }
        catch (Exception exception) when (
            exception is HttpRequestException or NotSupportedException or JsonException or OperationCanceledException)
        {
            logger.LogWarning(exception, "未能加载公共内容：{RequestUri}。", requestUri);
            return string.Empty;
        }
    }

    private static string NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
