using EzNutrition.Server.Extension;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EzNutrition.Server.Tests.Extension;

/// <summary>
/// 验证浏览器线程运行时所需的服务器响应约束。
/// </summary>
public sealed class ProgramExtensionTests
{
    /// <summary>
    /// 跨源隔离中间件应当为后续管道的响应写入完整且唯一的策略值。
    /// </summary>
    [Fact]
    public async Task UseCrossOriginIsolation_AddsRequiredResponseHeaders()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var app = new ApplicationBuilder(services);
        app.UseCrossOriginIsolation();
        app.Run(context => context.Response.WriteAsync("ok"));

        var context = new DefaultHttpContext();
        await app.Build()(context);

        Assert.Equal("same-origin", context.Response.Headers["Cross-Origin-Opener-Policy"]);
        Assert.Equal("require-corp", context.Response.Headers["Cross-Origin-Embedder-Policy"]);
    }
}
