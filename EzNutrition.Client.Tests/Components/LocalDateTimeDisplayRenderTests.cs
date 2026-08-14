using System.Net;
using EzNutrition.UI.Components;
using EzNutrition.UI.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EzNutrition.Client.Tests.Components;

public sealed class LocalDateTimeDisplayRenderTests
{
    [Fact]
    public async Task Utc_timestamp_is_rendered_in_the_host_time_zone_across_a_date_boundary()
    {
        var timeZone = TimeZoneInfo.CreateCustomTimeZone(
            "test-utc-plus-08",
            TimeSpan.FromHours(8),
            "Test UTC+08",
            "Test UTC+08");
        await using var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<ILocalDateTimeFormatter>(new LocalDateTimeFormatter(timeZone))
            .BuildServiceProvider();
        await using var renderer = new HtmlRenderer(
            services,
            services.GetRequiredService<ILoggerFactory>());
        var value = new DateTimeOffset(2026, 8, 14, 20, 30, 0, TimeSpan.Zero);

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<LocalDateTimeDisplay>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(LocalDateTimeDisplay.Value)] = value
                }));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });

        Assert.Contains("2026-08-15 04:30 +08:00", html, StringComparison.Ordinal);
        Assert.Contains("title=\"2026-08-15 04:30:00 +08:00\"", html, StringComparison.Ordinal);
        Assert.Contains(
            "datetime=\"2026-08-14T20:30:00.0000000+00:00\"",
            html,
            StringComparison.Ordinal);
    }
}
