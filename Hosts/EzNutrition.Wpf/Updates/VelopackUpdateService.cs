using EzNutrition.Wpf.Configuration;
using Velopack;

namespace EzNutrition.Wpf.Updates;

/// <summary>
/// 在非 MSIX 的 WPF 宿主中检查、下载并安排 Velopack 更新。
/// </summary>
internal sealed class VelopackUpdateService
{
    private readonly Uri updateFeedAddress;
    private UpdateManager? updateManager;
    private VelopackAsset? preparedUpdate;

    /// <summary>使用已经校验的宿主配置创建更新服务。</summary>
    public VelopackUpdateService(WpfHostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        updateFeedAddress = settings.UpdateFeedAddress;
    }

    /// <summary>
    /// 检查并下载最新版本；MSIX、调试输出等非 Velopack 安装环境直接跳过。
    /// </summary>
    internal async Task<ApplicationUpdateNotice?> CheckAndPrepareAsync(
        CancellationToken cancellationToken = default)
    {
        if (WindowsPackageIdentity.IsPackaged)
        {
            return null;
        }

        var manager = new UpdateManager(updateFeedAddress.AbsoluteUri);
        if (!manager.IsInstalled || manager.CurrentVersion is null)
        {
            return null;
        }

        var update = await manager.CheckForUpdatesAsync();
        if (update is null)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        await manager.DownloadUpdatesAsync(
            update,
            cancelToken: cancellationToken);

        updateManager = manager;
        preparedUpdate = update.TargetFullRelease;
        return new ApplicationUpdateNotice(
            manager.CurrentVersion.Version,
            update.TargetFullRelease.Version.Version);
    }

    /// <summary>
    /// 启动等待当前进程退出的更新器；调用方随后应走正常的应用关闭流程。
    /// </summary>
    internal void SchedulePreparedUpdateForRestart()
    {
        if (updateManager is null || preparedUpdate is null)
        {
            throw new InvalidOperationException("当前没有已经准备好的桌面更新。");
        }

        updateManager.WaitExitThenApplyUpdates(
            preparedUpdate,
            silent: false,
            restart: true);
    }
}
