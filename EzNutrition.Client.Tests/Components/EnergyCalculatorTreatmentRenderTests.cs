using System.Net;
using AntDesign;
using EzNutrition.Application.Consultations;
using EzNutrition.Application.Ports;
using EzNutrition.Domain.Assessments;
using EzNutrition.Domain.Consultations;
using EzNutrition.Shared.Data.Entities;
using EzNutrition.UI.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace EzNutrition.Client.Tests.Components;

public sealed class EnergyCalculatorTreatmentRenderTests
{
    [Fact]
    public async Task Missing_eer_data_offers_manual_energy_entry()
    {
        await using var services = BuildServiceProvider();
        await using var renderer = new HtmlRenderer(
            services,
            services.GetRequiredService<ILoggerFactory>());
        var calculator = new EnergyCalculator(new ClientInfo
        {
            Gender = "女",
            Age = new ChronologicalAge(0)
        });

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<EnergyCalculatorTreatment>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(EnergyCalculatorTreatment.EnergyCalculator)] = calculator
                }));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });

        Assert.Contains("没有可用的 EER 数据", html, StringComparison.Ordinal);
        Assert.Contains("请在下方手工填写", html, StringComparison.Ordinal);
        Assert.Contains("id=\"corEnergy\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("计算推荐能量", html, StringComparison.Ordinal);
    }

    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAntDesign();
        services.AddSingleton<IJSRuntime, NoOpJsRuntime>();
        var dataSource = new EmptyNutritionDataSource();
        services.AddSingleton(new ConsultationApplicationService(dataSource, dataSource, dataSource));
        return services.BuildServiceProvider();
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

    private sealed class EmptyNutritionDataSource :
        IEnergyReferenceDataSource,
        IDietaryReferenceIntakeDataSource,
        IFoodCompositionDataSource
    {
        public Task<IReadOnlyList<EER>> GetEnergyReferencesAsync(
            NutritionSubjectQuery subject,
            CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<EER>>([]);

        public Task<IReadOnlyList<DietaryReferenceIntakeValue>> GetDietaryReferenceIntakesAsync(
            NutritionSubjectQuery subject,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DietaryReferenceIntakeValue>>([]);

        public Task<IReadOnlyList<Food>> GetFoodsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Food>>([]);

        public Task<IReadOnlyList<Nutrient>> GetNutrientsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Nutrient>>([]);

        public Task<IReadOnlyList<FoodNutrientValue>> GetFoodCompositionAsync(
            string friendlyCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FoodNutrientValue>>([]);
    }
}
