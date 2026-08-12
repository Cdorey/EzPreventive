using AntDesign;
using EzNutrition.Application.Consultations;
using EzNutrition.Application.Ports;
using EzNutrition.Domain.Assessments;
using EzNutrition.Domain.Consultations;
using EzNutrition.Domain.Dietary;
using EzNutrition.Shared.Data.Entities;
using EzNutrition.UI.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using System.Net;

namespace EzNutrition.Client.Tests.Components;

public sealed class DietarySurveyRenderTests
{
    [Fact]
    public async Task EditorWithAnEntryRendersAccessibleDeleteButton()
    {
        await using var serviceProvider = BuildServiceProvider();
        await using var renderer = new Microsoft.AspNetCore.Components.Web.HtmlRenderer(
            serviceProvider,
            serviceProvider.GetRequiredService<ILoggerFactory>());
        var food = new Food
        {
            FoodId = Guid.NewGuid(),
            FriendlyCode = "TEST-001",
            FriendlyName = "测试食物",
            FoodNutrientValues = []
        };
        var client = new ClientInfo { Gender = "女" };
        var survey = new DietaryRecallSurvey(client, [food], [], new DRIs(client));
        survey.RecallEntries.Add(new DietaryRecallEntry
        {
            Food = food,
            Weight = 100m,
            IsAllEdible = true,
            MealOccasion = MealOccasion.Breakfast
        });

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<DietarySurvey>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(DietarySurvey.DietaryRecallSurvey)] = survey
                }));
            return output.ToHtmlString();
        });

        Assert.Contains(
            "aria-label=\"删除 测试食物\"",
            WebUtility.HtmlDecode(html),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task CalculatedResultRendersReferenceStatusTags()
    {
        await using var serviceProvider = BuildServiceProvider();
        await using var renderer = new Microsoft.AspNetCore.Components.Web.HtmlRenderer(
            serviceProvider,
            serviceProvider.GetRequiredService<ILoggerFactory>());
        var client = new ClientInfo { Gender = "男" };
        var survey = new DietaryRecallSurvey(client, [], [], new DRIs(client))
        {
            SummaryCalculationTable = new SummaryCalculationTable([], [])
        };
        survey.SummaryRows.AddRange(
        [
            new DietarySurveySummaryRow
            {
                FriendlyName = "总能量",
                ValueString = "1800",
                Unit = "kCal"
            },
            new DietarySurveySummaryRow
            {
                FriendlyName = "蛋白质供能比",
                ValueString = "8",
                Unit = "%E",
                ReferenceRange = "10~20",
                Flag = Flags.Lower
            }
        ]);

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<DietarySurvey>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(DietarySurvey.DietaryRecallSurvey)] = survey
                }));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });

        Assert.Contains("已完成核算", html, StringComparison.Ordinal);
        Assert.Contains("低于参考", html, StringComparison.Ordinal);
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
