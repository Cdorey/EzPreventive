using EzNutrition.Presentation.Services;

namespace EzNutrition.Client.Infrastructure;

/// <summary>
/// 声明浏览器宿主使用浏览器原生的新标签页承载辅助页面。
/// </summary>
internal sealed class BrowserAuxiliaryPageHost : IAuxiliaryPageHost
{
    /// <inheritdoc />
    public bool CanOpenInNativeWindow => false;

    /// <inheritdoc />
    public ValueTask OpenInNativeWindowAsync(AuxiliaryPage page) =>
        ValueTask.FromException(new NotSupportedException(
            "浏览器宿主不提供原生辅助窗口。"));
}
