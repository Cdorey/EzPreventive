using System.Net;
using AntDesign;
using EzNutrition.Application.Consultations;
using EzNutrition.Domain.Consultations;
using EzNutrition.Domain.Dietary;
using EzNutrition.Shared.Data.Entities;
using EzNutrition.UI.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace EzNutrition.Client.Tests.Components;

public sealed class SummaryRenderTests
{
    [Fact]
    public async Task Calculated_tower_offers_confirmed_soap_import()
    {
        await using var services = new ServiceCollection()
            .AddLogging()
            .AddAntDesign()
            .AddSingleton<IJSRuntime, NoOpJsRuntime>()
            .BuildServiceProvider();
        await using var renderer = new HtmlRenderer(
            services,
            services.GetRequiredService<ILoggerFactory>());
        var standard = StandardTower.GetStandardTower(18m)!;
        var food = new Food
        {
            FoodId = Guid.NewGuid(),
            FriendlyCode = "TEST-TOWER",
            FriendlyName = "测试食物",
            FoodGroups = "动物性食品",
            FoodNutrientValues = []
        };
        var workspace = new ConsultationWorkspace(new ClientInfo())
        {
            DietaryTower = new DietaryRecallTower(
            [
                new DietaryRecallEntry
                {
                    Food = food,
                    Weight = 100m,
                    MealOccasion = MealOccasion.Lunch
                }
            ],
            standard)
        };

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<Summary>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(Summary.Archive)] = workspace,
                    [nameof(Summary.OnSoapContributionConfirmed)] =
                        EventCallback.Factory.Create<SoapContribution>(new object(), _ => { })
                }));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });

        Assert.Contains("膳食平衡宝塔", html, StringComparison.Ordinal);
        Assert.Contains("引入 SOAP", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reference_tower_without_a_contribution_does_not_render_import_control()
    {
        await using var services = new ServiceCollection()
            .AddLogging()
            .AddAntDesign()
            .AddSingleton<IJSRuntime, NoOpJsRuntime>()
            .BuildServiceProvider();
        await using var renderer = new HtmlRenderer(
            services,
            services.GetRequiredService<ILoggerFactory>());
        var workspace = new ConsultationWorkspace(new ClientInfo())
        {
            DietaryTower = StandardTower.GetStandardTower(18m)
        };

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<Summary>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(Summary.Archive)] = workspace,
                    [nameof(Summary.OnSoapContributionConfirmed)] =
                        EventCallback.Factory.Create<SoapContribution>(new object(), _ => { })
                }));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });

        Assert.DoesNotContain("引入 SOAP", html, StringComparison.Ordinal);
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
