using System.Net;
using AntDesign;
using EzNutrition.Application.Archives;
using EzNutrition.UI.Components;
using EzNutrition.UI.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace EzNutrition.Client.Tests.Components;

public sealed class ArchiveDocumentReviewRenderTests
{
    [Fact]
    public async Task Only_sections_with_projected_details_render_a_detail_action()
    {
        var review = new ArchiveReview
        {
            BundleId = Guid.NewGuid(),
            Title = "量表详情测试档案",
            SubjectDisplay = "虚构咨询对象",
            CreatedAt = new DateTimeOffset(2026, 8, 31, 8, 0, 0, TimeSpan.Zero),
            FormatDisplay = "测试档案格式",
            ContainsUnknownContent = false,
            Sections =
            [
                new ArchiveReviewSection
                {
                    Title = "虚构营养量表",
                    Fields = [new ArchiveReviewField("总分", "1")],
                    DetailGroups =
                    [
                        new ArchiveReviewDetailGroup
                        {
                            Title = "逐题作答",
                            Fields = [new ArchiveReviewField("虚构问题", "虚构答案（本题 1 分）")]
                        }
                    ]
                },
                new ArchiveReviewSection
                {
                    Title = "SOAP 病史",
                    Fields = [new ArchiveReviewField("主观资料", "未记录")]
                }
            ]
        };

        var html = WebUtility.HtmlDecode(await RenderAsync(review));

        Assert.Equal(1, CountOccurrences(html, "查看详情"));
        Assert.Contains("review-detail-action", html, StringComparison.Ordinal);
        Assert.Contains("虚构营养量表", html, StringComparison.Ordinal);
        Assert.Contains("SOAP 病史", html, StringComparison.Ordinal);
    }

    private static async Task<string> RenderAsync(ArchiveReview review)
    {
        await using var services = new ServiceCollection()
            .AddLogging()
            .AddAntDesign()
            .AddSingleton<IJSRuntime, NoOpJsRuntime>()
            .AddSingleton<ILocalDateTimeFormatter>(new LocalDateTimeFormatter(TimeZoneInfo.Utc))
            .BuildServiceProvider();
        await using var renderer = new HtmlRenderer(
            services,
            services.GetRequiredService<ILoggerFactory>());

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<ArchiveDocumentReview>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(ArchiveDocumentReview.Review)] = review
                }));
            return output.ToHtmlString();
        });
    }

    private static int CountOccurrences(string value, string expected) =>
        value.Split(expected, StringSplitOptions.None).Length - 1;

    private sealed class NoOpJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            ValueTask.FromResult(default(TValue)!);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args) => ValueTask.FromResult(default(TValue)!);
    }
}
