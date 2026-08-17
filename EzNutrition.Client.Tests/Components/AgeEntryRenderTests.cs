using System.Net;
using AntDesign;
using EzNutrition.Domain.Consultations;
using EzNutrition.UI.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace EzNutrition.Client.Tests.Components;

public sealed class AgeEntryRenderTests
{
    [Fact]
    public async Task Birth_date_mode_derives_and_displays_the_composite_age()
    {
        var client = new ClientInfo
        {
            BirthDate = new DateOnly(2024, 4, 17),
            Age = null
        };
        var html = await RenderAsync(client, new DateOnly(2025, 9, 9));

        Assert.Equal(new ChronologicalAge(1, 4, 23), client.Age);
        Assert.Contains("1岁4个月23天", html, StringComparison.Ordinal);
        Assert.Contains("id=\"birth-date\"", html, StringComparison.Ordinal);
        Assert.Contains("type=\"date\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"age\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reported_year_mode_shows_only_the_integer_age_input()
    {
        var client = new ClientInfo { Age = new ChronologicalAge(25) };

        var html = await RenderAsync(client, new DateOnly(2025, 9, 9));

        Assert.Null(client.BirthDate);
        Assert.Equal(new ChronologicalAge(25), client.Age);
        Assert.Contains("id=\"age\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"birth-date\"", html, StringComparison.Ordinal);
    }

    private static async Task<string> RenderAsync(ClientInfo client, DateOnly effectiveDate)
    {
        await using var services = new ServiceCollection()
            .AddLogging()
            .AddAntDesign()
            .AddSingleton<IJSRuntime, NoOpJsRuntime>()
            .BuildServiceProvider();
        await using var renderer = new HtmlRenderer(
            services,
            services.GetRequiredService<ILoggerFactory>());

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            RenderFragment<ClientInfo> content = _ => builder =>
            {
                builder.OpenComponent<AgeEntry>(0);
                builder.AddAttribute(1, nameof(AgeEntry.Client), client);
                builder.AddAttribute(2, nameof(AgeEntry.EffectiveDate), effectiveDate);
                builder.CloseComponent();
            };
            var output = await renderer.RenderComponentAsync<Form<ClientInfo>>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(Form<ClientInfo>.Model)] = client,
                    [nameof(Form<ClientInfo>.ChildContent)] = content
                }));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });

        return html;
    }

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
