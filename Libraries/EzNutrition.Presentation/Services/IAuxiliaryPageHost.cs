namespace EzNutrition.Presentation.Services;

/// <summary>
/// 描述当前客户端宿主承载辅助页面的能力。
/// </summary>
/// <remarks>
/// 浏览器宿主可以保留链接的原生新标签页语义；具备原生多窗口能力的宿主则实现
/// <see cref="OpenInNativeWindowAsync"/>，由共享组件在用户点击时显式调用。
/// </remarks>
public interface IAuxiliaryPageHost
{
    /// <summary>获取当前宿主能否在原生辅助窗口中承载页面。</summary>
    bool CanOpenInNativeWindow { get; }

    /// <summary>
    /// 在原生辅助窗口中打开指定页面。
    /// </summary>
    /// <param name="page">需要打开的辅助页面。</param>
    /// <returns>表示打开操作的异步任务。</returns>
    ValueTask OpenInNativeWindowAsync(AuxiliaryPage page);
}
