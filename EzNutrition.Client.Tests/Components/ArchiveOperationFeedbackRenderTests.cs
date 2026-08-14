using System.Net;
using EzNutrition.Application.Archives;
using EzNutrition.UI.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EzNutrition.Client.Tests.Components;

public sealed class ArchiveOperationFeedbackRenderTests
{
    [Fact]
    public async Task Blocking_notices_render_the_safe_message_and_stable_code()
    {
        var result = new ArchiveOperationResult
        {
            Status = ArchiveOperationStatus.Invalid,
            Message = "当前咨询未通过档案校验，尚未写出。",
            Notices =
            [
                new ArchiveNotice
                {
                    Code = "archive.nutrition.aggregation-mismatch",
                    IsBlocking = true,
                    Message = "保存的汇总无法由组成项复算。"
                }
            ]
        };

        var html = WebUtility.HtmlDecode(await RenderAsync(result));

        Assert.Contains("role=\"alert\"", html, StringComparison.Ordinal);
        Assert.Contains(result.Message, html, StringComparison.Ordinal);
        Assert.Contains(result.Notices[0].Message, html, StringComparison.Ordinal);
        Assert.Contains(result.Notices[0].Code, html, StringComparison.Ordinal);
        Assert.Contains("阻断写出", html, StringComparison.Ordinal);
        Assert.Contains("class=\"blocking\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Notice_content_is_html_encoded()
    {
        var result = new ArchiveOperationResult
        {
            Status = ArchiveOperationStatus.Invalid,
            Message = "档案校验失败。",
            Notices =
            [
                new ArchiveNotice
                {
                    Code = "archive.test.<unsafe>",
                    IsBlocking = true,
                    Message = "<script>alert('unsafe')</script>"
                }
            ]
        };

        var html = await RenderAsync(result);

        Assert.DoesNotContain("<script>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
        Assert.Contains("archive.test.&lt;unsafe&gt;", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Denied_operation_renders_as_a_warning_status()
    {
        var result = new ArchiveOperationResult
        {
            Status = ArchiveOperationStatus.Denied,
            Message = "当前策略不允许删除档案。"
        };

        var html = WebUtility.HtmlDecode(await RenderAsync(result));

        Assert.Contains("class=\"archive-operation-feedback feedback-warning\"", html, StringComparison.Ordinal);
        Assert.Contains("role=\"status\"", html, StringComparison.Ordinal);
        Assert.Contains(result.Message, html, StringComparison.Ordinal);
    }

    private static async Task<string> RenderAsync(ArchiveOperationResult result)
    {
        await using var services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        await using var renderer = new HtmlRenderer(
            services,
            services.GetRequiredService<ILoggerFactory>());

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<ArchiveOperationFeedback>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(ArchiveOperationFeedback.Result)] = result
                }));
            return output.ToHtmlString();
        });
    }
}
