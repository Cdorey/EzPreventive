namespace EzNutrition.Client.Infrastructure;

/// <summary>
/// 验证当前客户端运行时会把 <see cref="Task.Run(Action)"/> 调度到主线程之外。
/// </summary>
internal static class ThreadingRuntimeGuard
{
    /// <summary>
    /// 执行一次最小线程池探针；未获得独立后台线程时使客户端启动失败。
    /// </summary>
    /// <exception cref="PlatformNotSupportedException">线程池未能及时调度到独立后台线程。</exception>
    public static async Task EnsureBackgroundThreadAsync()
    {
        var callingThreadId = Environment.CurrentManagedThreadId;
        var probe = Task.Run(() => Environment.CurrentManagedThreadId);
        var completed = await Task.WhenAny(probe, Task.Delay(TimeSpan.FromSeconds(10)));
        if (completed != probe)
        {
            throw new PlatformNotSupportedException("后台线程未能在预期时间内启动。");
        }

        var backgroundThreadId = await probe;
        if (backgroundThreadId == callingThreadId)
        {
            throw new PlatformNotSupportedException("当前浏览器没有提供应用所需的后台线程能力。");
        }
    }
}
