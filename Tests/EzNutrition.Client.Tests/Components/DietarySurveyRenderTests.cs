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
        survey.NutrientAssessments.AddRange(
        [
            new DietaryNutrientAssessment
            {
                FriendlyName = "总能量",
                Value = 1800m,
                Unit = "kCal"
            },
            new DietaryNutrientAssessment
            {
                FriendlyName = "蛋白质供能比",
                Value = 8m,
                Unit = "%E",
                LowerReference = new DietaryNutrientReference(
                    DietaryReferenceIntakeType.AMDR_L,
                    10m,
                    "%E"),
                UpperReference = new DietaryNutrientReference(
                    DietaryReferenceIntakeType.AMDR_H,
                    20m,
                    "%E")
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
        Assert.Contains("单日 24 小时膳食回顾", html, StringComparison.Ordinal);
        Assert.Contains("进一步问询的风险线索", html, StringComparison.Ordinal);
        Assert.Contains("不单独构成营养诊断", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Calculated_recall_offers_confirmed_soap_import()
    {
        await using var serviceProvider = BuildServiceProvider();
        await using var renderer = new Microsoft.AspNetCore.Components.Web.HtmlRenderer(
            serviceProvider,
            serviceProvider.GetRequiredService<ILoggerFactory>());
        var client = new ClientInfo { Gender = "女" };
        var energy = new Nutrient
        {
            NutrientId = 1,
            FriendlyName = "能量",
            DefaultMeasureUnit = "kCal"
        };
        var food = new Food
        {
            FoodId = Guid.NewGuid(),
            FriendlyCode = "TEST-SOAP",
            FriendlyName = "测试食物"
        };
        food.FoodNutrientValues =
        [
            new FoodNutrientValue
            {
                Food = food,
                FoodId = food.FoodId,
                Nutrient = energy,
                NutrientId = energy.NutrientId,
                MeasureUnit = energy.DefaultMeasureUnit,
                Value = 100m
            }
        ];
        var survey = new DietaryRecallSurvey(client, [food], [energy], new DRIs(client));
        survey.RecallEntries.Add(new DietaryRecallEntry
        {
            Food = food,
            Weight = 100m,
            MealOccasion = MealOccasion.Lunch
        });
        survey.SummaryCalculationTable = new SummaryCalculationTable(
            survey.RecallEntries,
            [energy]);

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<DietarySurvey>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(DietarySurvey.DietaryRecallSurvey)] = survey,
                    [nameof(DietarySurvey.OnSoapContributionConfirmed)] =
                        EventCallback.Factory.Create<SoapContribution>(new object(), _ => { })
                }));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });

        Assert.Contains("引入 SOAP", html, StringComparison.Ordinal);
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
