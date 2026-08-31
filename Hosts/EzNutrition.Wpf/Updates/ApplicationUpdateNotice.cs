namespace EzNutrition.Wpf.Updates;

/// <summary>
/// 描述已经下载并等待应用的桌面版本更新。
/// </summary>
/// <param name="CurrentVersion">当前安装版本。</param>
/// <param name="TargetVersion">已经准备好的目标版本。</param>
internal sealed record ApplicationUpdateNotice(
    Version CurrentVersion,
    Version TargetVersion)
{
    /// <summary>
    /// 获取更新是否跨越产品代际或 HTTP 接口契约代际。
    /// </summary>
    internal bool ChangesCompatibilityLine =>
        CurrentVersion.Major != TargetVersion.Major ||
        CurrentVersion.Minor != TargetVersion.Minor;
}
