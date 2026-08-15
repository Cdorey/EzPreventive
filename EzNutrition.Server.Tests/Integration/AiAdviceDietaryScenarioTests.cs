using AntDesign;
using EzNutrition.Application.Consultations;
using EzNutrition.Application.Ports;
using EzNutrition.Client.Infrastructure;
using EzNutrition.Domain.Assessments;
using EzNutrition.Domain.Consultations;
using EzNutrition.Domain.Dietary;
using EzNutrition.Server.Services;
using EzNutrition.Shared.Data.DTO.PromptDto;
using EzNutrition.Shared.Data.Entities;
using EzNutrition.UI.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit.Abstractions;
using AdviceDietarySurvey = EzNutrition.Shared.Data.DTO.PromptDto.DietaryRecallSurvey;
using AdviceReferenceComparison = EzNutrition.Shared.Data.DTO.PromptDto.DietaryReferenceComparison;
using DomainDietarySurvey = EzNutrition.Domain.Dietary.DietaryRecallSurvey;

namespace EzNutrition.Server.Tests.Integration;

/// <summary>
/// Exercises representative synthetic diets across calculation, UI, HTTP and model-message boundaries.
/// No request leaves the test process and no model is invoked.
/// </summary>
public sealed class AiAdviceDietaryScenarioTests(ITestOutputHelper output)
{
    private static readonly Uri BaseAddress = new("https://eznutrition.test/");

    public static TheoryData<string> ScenarioKeys => new()
    {
        "balanced-day",
        "takeaway-heavy-day",
        "light-intake-day"
    };

    [Theory]
    [MemberData(nameof(ScenarioKeys))]
    public async Task Scenario_flows_from_dietary_ui_to_http_and_model_messages(string scenarioKey)
    {
        var scenario = await CreateScenarioAsync(scenarioKey);
        var survey = scenario.Workspace.DietaryRecallSurvey!;
        var handler = new RecordingHandler();
        var gateway = new HttpAiAdviceGateway(new RecordingHttpClientFactory(handler));
        var adviceService = new AiAdviceApplicationService(gateway);

        Assert.True(adviceService.PreparePrompt(scenario.Workspace));
        var preparedRequest = Assert.IsType<AiAdviceRequestDto>(scenario.Workspace.AdvicePrompt);
        var uiHtml = await RenderDietarySurveyAsync(survey);

        await foreach (var _ in adviceService.GenerateAsync(scenario.Workspace, environment: null))
        {
        }

        var httpJson = Assert.IsType<string>(handler.RequestBody);
        var postedRequest = JsonSerializer.Deserialize<AiAdviceRequestDto>(
            httpJson,
            AiAdviceJson.Compact);
        Assert.NotNull(postedRequest);
        var modelPrompt = new AiAdvicePromptComposer().Compose(postedRequest);

        AssertUiProjection(scenario, uiHtml);
        AssertPreparedDietaryProjection(scenario, preparedRequest);
        AssertHttpDisclosureShape(scenario, httpJson);
        Assert.Equal(AiAdviceJson.Serialize(preparedRequest), httpJson);
        Assert.Equal(httpJson, AiAdviceJson.Serialize(postedRequest));
        AssertModelMessages(scenario, httpJson, modelPrompt.SystemMessage, modelPrompt.UserMessage);

        output.WriteLine($"===== {scenario.Key}: HTTP request =====");
        output.WriteLine(JsonSerializer.Serialize(postedRequest, AiAdviceJson.Indented));
        output.WriteLine($"===== {scenario.Key}: LLM system message =====");
        output.WriteLine(modelPrompt.SystemMessage);
        output.WriteLine($"===== {scenario.Key}: LLM user message =====");
        output.WriteLine(modelPrompt.UserMessage);
    }

    private static void AssertUiProjection(DietaryScenario scenario, string uiHtml)
    {
        Assert.Contains("24 小时回顾法膳食调查", uiHtml, StringComparison.Ordinal);
        Assert.Contains("已完成核算", uiHtml, StringComparison.Ordinal);
        Assert.Contains($"{scenario.Entries.Count} 项食物", uiHtml, StringComparison.Ordinal);
        foreach (var entry in scenario.Entries)
        {
            Assert.Contains(entry.FoodName, uiHtml, StringComparison.Ordinal);
        }

        foreach (var comparison in scenario.ExpectedNutrients.Values
            .Select(nutrient => nutrient.Comparison)
            .Distinct())
        {
            Assert.Contains(ComparisonLabel(comparison), uiHtml, StringComparison.Ordinal);
        }
    }

    private static void AssertPreparedDietaryProjection(
        DietaryScenario scenario,
        AiAdviceRequestDto request)
    {
        Assert.Equal(AiAdviceRequestDto.CurrentSchemaVersion, request.SchemaVersion);
        var dietary = Assert.IsType<AdviceDietarySurvey>(request.DietaryRecallSurvey);
        Assert.Equal("24-hour-recall", dietary.Method);
        Assert.Equal(1, dietary.RecallDays);
        Assert.Equal(scenario.Entries.Count, dietary.Foods.Length);

        Assert.Collection(
            dietary.Foods,
            scenario.Entries.Select<ScenarioEntry, Action<DietaryRecallFoodItem>>(expected => actual =>
            {
                Assert.Equal(expected.FoodName, actual.FoodName);
                Assert.Equal(MapMeal(expected.Meal), actual.Meal);
                Assert.Equal(expected.ExpectedEdibleAmount, actual.EdibleAmount);
                Assert.Equal("g", actual.Unit);
            }).ToArray());

        foreach (var expected in scenario.ExpectedNutrients)
        {
            var nutrient = Assert.Single(
                dietary.Nutrients,
                item => item.Name == expected.Key);
            Assert.Equal(expected.Value.Intake, nutrient.Intake);
            Assert.Equal(expected.Value.Comparison, nutrient.ReferenceComparison);
        }

        var sodium = Assert.Single(dietary.Nutrients, nutrient => nutrient.Name == "钠");
        Assert.Contains(
            sodium.References,
            reference => reference.Type == "PI-NCD"
                && reference.Value == 2000m
                && reference.Unit == "mg/d");

        var totalEnergy = Assert.Single(
            dietary.Nutrients,
            nutrient => nutrient.Name == "总能量");
        Assert.NotNull(totalEnergy.MealEnergyShares);
        Assert.NotEmpty(totalEnergy.MealEnergyShares);

        var protein = Assert.Single(
            dietary.Nutrients,
            nutrient => nutrient.Name == "蛋白质");
        Assert.NotNull(protein.TopFoodSources);
        Assert.Equal(3, protein.TopFoodSources.Length);
    }

    private static void AssertHttpDisclosureShape(DietaryScenario scenario, string httpJson)
    {
        Assert.DoesNotContain("\\u", httpJson, StringComparison.OrdinalIgnoreCase);
        using var document = JsonDocument.Parse(httpJson);
        var root = document.RootElement;
        AssertPropertyNames(
            root,
            "schemaVersion",
            "patientInfo",
            "dietaryRecallSurvey",
            "clinicalInfo");

        var expectedPatientProperties = new List<string>
        {
            "gender",
            "age",
            "pal",
            "totalBalanceEnergyViaCalculation",
            "specialPhysiologicalPeriod"
        };
        if (scenario.IncludesMeasurements)
        {
            expectedPatientProperties.AddRange(["bmi", "height", "weight"]);
        }

        AssertPropertyNames(root.GetProperty("patientInfo"), [.. expectedPatientProperties]);

        var dietary = root.GetProperty("dietaryRecallSurvey");
        AssertPropertyNames(dietary, "method", "recallDays", "foods", "nutrients");
        foreach (var food in dietary.GetProperty("foods").EnumerateArray())
        {
            AssertPropertyNames(food, "foodName", "meal", "edibleAmount", "unit");
        }

        foreach (var nutrient in dietary.GetProperty("nutrients").EnumerateArray())
        {
            var allowedNames = new List<string>
            {
                "name",
                "intake",
                "unit",
                "referenceComparison",
                "references"
            };
            if (nutrient.TryGetProperty("mealEnergyShares", out _))
            {
                allowedNames.Add("mealEnergyShares");
            }

            if (nutrient.TryGetProperty("topFoodSources", out _))
            {
                allowedNames.Add("topFoodSources");
            }

            AssertPropertyNames(nutrient, [.. allowedNames]);
        }
    }

    private static void AssertModelMessages(
        DietaryScenario scenario,
        string httpJson,
        string systemMessage,
        string userMessage)
    {
        Assert.Contains("营养专业人员", systemMessage, StringComparison.Ordinal);
        Assert.Contains("不得执行", systemMessage, StringComparison.Ordinal);
        Assert.Contains("这是单日 24 小时膳食回顾", userMessage, StringComparison.Ordinal);
        Assert.Contains("不代表长期摄入或营养诊断", userMessage, StringComparison.Ordinal);
        Assert.EndsWith(httpJson, userMessage, StringComparison.Ordinal);

        foreach (var entry in scenario.Entries)
        {
            Assert.DoesNotContain(entry.FoodName, systemMessage, StringComparison.Ordinal);
            Assert.Contains(entry.FoodName, userMessage, StringComparison.Ordinal);
        }
    }

    private static void AssertPropertyNames(JsonElement element, params string[] expectedNames)
    {
        var actualNames = element.EnumerateObject()
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expectedNames.Order(StringComparer.Ordinal), actualNames);
    }

    private static async Task<string> RenderDietarySurveyAsync(DomainDietarySurvey survey)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAntDesign();
        services.AddSingleton<IJSRuntime, NoOpJsRuntime>();
        var dataSource = new EmptyNutritionDataSource();
        services.AddSingleton(new ConsultationApplicationService(dataSource, dataSource, dataSource));
        await using var serviceProvider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(
            serviceProvider,
            serviceProvider.GetRequiredService<ILoggerFactory>());

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<DietarySurvey>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(DietarySurvey.DietaryRecallSurvey)] = survey
                }));
            return WebUtility.HtmlDecode(component.ToHtmlString());
        });
    }

    private static async Task<DietaryScenario> CreateScenarioAsync(string key)
    {
        var definition = ScenarioDefinitions.Single(scenario => scenario.Key == key);
        var client = new ClientInfo
        {
            Name = $"合成咨询对象·{definition.Key}",
            Gender = "女",
            Age = 35,
            Height = definition.IncludesMeasurements ? 165m : null,
            Weight = definition.IncludesMeasurements ? 60m : null
        };
        var energy = new EnergyCalculator(client)
        {
            PAL = 1.5m,
            AvailableEERs = [new EER { BEE = 21m, PAL = 1.5m, AvgBwEER = 2000 }]
        };
        Assert.True(energy.Calculate());
        Assert.True(energy.CorrectEnergy(2000));

        var nutrients = CreateNutrients();
        var foods = definition.EntryDefinitions
            .Select(entry => entry.Food)
            .DistinctBy(food => food.Code)
            .Select((food, index) => CreateFood(food, index + 1, nutrients))
            .ToArray();
        var dris = new DRIs(client)
        {
            AvailableDRIs = CreateReferenceIntakes(client)
        };
        var survey = new DomainDietarySurvey(client, foods, nutrients, dris);
        survey.RecallEntries.AddRange(definition.EntryDefinitions.Select((entry, index) =>
        {
            var food = foods.Single(item => item.FriendlyCode == entry.Food.Code);
            return new DietaryRecallEntry
            {
                EntryId = Guid.Parse($"81000000-0000-0000-0000-{index + 1:x12}"),
                Food = food,
                Weight = entry.Weight,
                MealOccasion = entry.Meal,
                IsAllEdible = entry.IsAllEdible
            };
        }));
        var dataSource = new EmptyNutritionDataSource();
        var consultationService = new ConsultationApplicationService(
            dataSource,
            dataSource,
            dataSource);
        await consultationService.CalculateDietaryRecallAsync(survey);

        var workspace = new ConsultationWorkspace(client)
        {
            CurrentEnergyCalculator = energy,
            DRIs = dris,
            DietaryRecallSurvey = survey,
            SubjectiveObjectiveAssessmentPlanInformation = new()
            {
                Subjective = $"{definition.DisplayName}的合成 24 小时膳食回顾。",
                Objective = "食物名称、餐次和重量均为测试数据。",
                Assessment = "等待程序核算结果和专业人员复核。",
                Plan = "仅用于验证数据传输边界。"
            }
        };
        var entries = definition.EntryDefinitions.Select(entry => new ScenarioEntry(
                entry.Food.Name,
                entry.Meal,
                entry.IsAllEdible
                    ? entry.Weight
                    : entry.Weight * entry.Food.EdiblePortion / 100m))
            .ToArray();

        return new DietaryScenario(
            definition.Key,
            workspace,
            entries,
            definition.IncludesMeasurements,
            definition.ExpectedNutrients);
    }

    private static List<Nutrient> CreateNutrients()
    {
        var definitions = new (string Name, string Unit)[]
        {
            ("能量", "kcal"),
            ("蛋白质", "g"),
            ("脂肪", "g"),
            ("碳水化合物", "g"),
            ("钾", "mg"),
            ("钠", "mg"),
            ("镁", "mg"),
            ("铁", "mg"),
            ("锰", "mg"),
            ("锌", "mg"),
            ("磷", "mg"),
            ("硒", "μg"),
            ("铜", "mg"),
            ("总维生素A", "μg RAE"),
            ("视黄醇", "μg"),
            ("胡萝卜素", "μg"),
            ("硫胺素", "mg"),
            ("核黄素", "mg"),
            ("烟酸", "mg"),
            ("维生素C", "mg"),
            ("总维生素E", "mg α-TE")
        };

        return definitions.Select((definition, index) => new Nutrient
        {
            NutrientId = index + 1,
            FriendlyName = definition.Name,
            DefaultMeasureUnit = definition.Unit
        }).ToList();
    }

    private static Food CreateFood(
        FoodDefinition definition,
        int sequence,
        IReadOnlyList<Nutrient> nutrients)
    {
        var food = new Food
        {
            FoodId = Guid.Parse($"82000000-0000-0000-0000-{sequence:x12}"),
            FriendlyCode = definition.Code,
            FriendlyName = definition.Name,
            EdiblePortion = definition.EdiblePortion,
            FoodGroups = "合成测试食物"
        };
        food.FoodNutrientValues =
        [
            FoodValue(food, nutrients, "能量", definition.Energy),
            FoodValue(food, nutrients, "蛋白质", definition.Protein),
            FoodValue(food, nutrients, "脂肪", definition.Fat),
            FoodValue(food, nutrients, "碳水化合物", definition.Carbohydrate),
            FoodValue(food, nutrients, "钠", definition.Sodium)
        ];
        return food;
    }

    private static FoodNutrientValue FoodValue(
        Food food,
        IReadOnlyList<Nutrient> nutrients,
        string nutrientName,
        decimal value)
    {
        var nutrient = nutrients.Single(item => item.FriendlyName == nutrientName);
        return new FoodNutrientValue
        {
            Food = food,
            FoodId = food.FoodId,
            Nutrient = nutrient,
            NutrientId = nutrient.NutrientId,
            MeasureUnit = nutrient.DefaultMeasureUnit,
            Value = value
        };
    }

    private static List<DietaryReferenceIntakeValue> CreateReferenceIntakes(IClient client) =>
    [
        Dri(client, "蛋白质", DietaryReferenceIntakeType.RNI, 60m, "g/d"),
        Dri(client, "总脂肪", DietaryReferenceIntakeType.AMDR_L, 20m, "%E"),
        Dri(client, "总脂肪", DietaryReferenceIntakeType.AMDR_H, 30m, "%E"),
        Dri(client, "碳水化合物", DietaryReferenceIntakeType.AMDR_L, 50m, "%E"),
        Dri(client, "碳水化合物", DietaryReferenceIntakeType.AMDR_H, 65m, "%E"),
        Dri(client, "钠", DietaryReferenceIntakeType.AI, 1500m, "mg/d"),
        Dri(client, "钠", DietaryReferenceIntakeType.UL, 2000m, "mg/d"),
        Dri(client, "钠", DietaryReferenceIntakeType.PI_NCD, 2000m, "mg/d")
    ];

    private static DietaryReferenceIntakeValue Dri(
        IClient client,
        string nutrient,
        DietaryReferenceIntakeType type,
        decimal value,
        string unit) => new()
        {
            Nutrient = nutrient,
            RecordType = type,
            Value = value,
            MeasureUnit = unit,
            Gender = client.Gender,
            AgeStart = 18,
            Detail = "合成测试参考值"
        };

    private static string ComparisonLabel(AdviceReferenceComparison comparison) => comparison switch
    {
        AdviceReferenceComparison.WithinReference => "参考范围内",
        AdviceReferenceComparison.BelowReference => "低于参考",
        AdviceReferenceComparison.AboveReference => "高于参考",
        AdviceReferenceComparison.NotEstablished => "无参考值",
        _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null)
    };

    private static DietaryMealOccasion MapMeal(MealOccasion meal) => meal switch
    {
        MealOccasion.Breakfast => DietaryMealOccasion.Breakfast,
        MealOccasion.MorningSnack => DietaryMealOccasion.MorningSnack,
        MealOccasion.Lunch => DietaryMealOccasion.Lunch,
        MealOccasion.AfternoonSnack => DietaryMealOccasion.AfternoonSnack,
        MealOccasion.Dinner => DietaryMealOccasion.Dinner,
        MealOccasion.LateNightSnack => DietaryMealOccasion.LateNightSnack,
        _ => throw new ArgumentOutOfRangeException(nameof(meal), meal, null)
    };

    private static IReadOnlyList<DietaryScenarioDefinition> ScenarioDefinitions { get; } =
    [
        new DietaryScenarioDefinition(
            "balanced-day",
            "三餐相对均衡",
            true,
            [
                Entry(OatPorridge, 300m, MealOccasion.Breakfast),
                Entry(Egg, 50m, MealOccasion.Breakfast),
                Entry(Rice, 150m, MealOccasion.Lunch),
                Entry(ChickenBreast, 150m, MealOccasion.Lunch),
                Entry(Broccoli, 250m, MealOccasion.Dinner),
                Entry(Rice, 150m, MealOccasion.Dinner),
                Entry(Milk, 250m, MealOccasion.LateNightSnack)
            ],
            new Dictionary<string, ExpectedNutrient>
            {
                ["总能量"] = new(1054.5m, AdviceReferenceComparison.NotEstablished),
                ["蛋白质"] = new(68.80m, AdviceReferenceComparison.WithinReference),
                ["脂肪供能比"] = new(23m, AdviceReferenceComparison.WithinReference),
                ["碳水化合物供能比"] = new(52m, AdviceReferenceComparison.WithinReference),
                ["钠"] = new(1628m, AdviceReferenceComparison.WithinReference)
            }),
        new DietaryScenarioDefinition(
            "takeaway-heavy-day",
            "外卖和加工食品偏多",
            true,
            [
                Entry(FriedDoughStick, 100m, MealOccasion.Breakfast),
                Entry(InstantNoodles, 120m, MealOccasion.Lunch),
                Entry(FriedChicken, 180m, MealOccasion.Dinner),
                Entry(PickledVegetables, 100m, MealOccasion.Dinner),
                Entry(SweetDrink, 500m, MealOccasion.AfternoonSnack)
            ],
            new Dictionary<string, ExpectedNutrient>
            {
                ["总能量"] = new(1645.6m, AdviceReferenceComparison.NotEstablished),
                ["蛋白质"] = new(52.2m, AdviceReferenceComparison.BelowReference),
                ["脂肪供能比"] = new(39m, AdviceReferenceComparison.AboveReference),
                ["碳水化合物供能比"] = new(49m, AdviceReferenceComparison.BelowReference),
                ["钠"] = new(7270m, AdviceReferenceComparison.AboveReference)
            }),
        new DietaryScenarioDefinition(
            "light-intake-day",
            "全天摄入偏少",
            false,
            [
                Entry(RicePorridge, 300m, MealOccasion.Breakfast),
                Entry(Apple, 150m, MealOccasion.MorningSnack, isAllEdible: false),
                Entry(GreenVegetables, 100m, MealOccasion.Lunch),
                Entry(Tofu, 100m, MealOccasion.Dinner),
                Entry(LowFatMilk, 200m, MealOccasion.LateNightSnack)
            ],
            new Dictionary<string, ExpectedNutrient>
            {
                ["总能量"] = new(447.575m, AdviceReferenceComparison.NotEstablished),
                ["蛋白质"] = new(18.41m, AdviceReferenceComparison.BelowReference),
                ["脂肪供能比"] = new(17m, AdviceReferenceComparison.BelowReference),
                ["碳水化合物供能比"] = new(55m, AdviceReferenceComparison.WithinReference),
                ["钠"] = new(398.55m, AdviceReferenceComparison.BelowReference)
            })
    ];

    private static FoodDefinition OatPorridge =>
        new("SYN-001", "燕麦粥", 100, 70m, 2.5m, 1.4m, 12m, 120m);
    private static FoodDefinition Egg =>
        new("SYN-002", "鸡蛋", 100, 144m, 13.3m, 8.8m, 2.8m, 131m);
    private static FoodDefinition Rice =>
        new("SYN-003", "米饭", 100, 116m, 2.6m, 0.3m, 25.9m, 20m);
    private static FoodDefinition ChickenBreast =>
        new("SYN-004", "鸡胸肉", 100, 133m, 19.4m, 5m, 2.5m, 300m);
    private static FoodDefinition Broccoli =>
        new("SYN-005", "西兰花", 100, 36m, 4.1m, 0.6m, 4.3m, 240m);
    private static FoodDefinition Milk =>
        new("SYN-006", "牛奶", 100, 54m, 3m, 3.2m, 3.4m, 37m);
    private static FoodDefinition FriedDoughStick =>
        new("SYN-007", "油条", 100, 388m, 6.9m, 17.6m, 51m, 585m);
    private static FoodDefinition InstantNoodles =>
        new("SYN-008", "方便面", 100, 473m, 9.5m, 21.1m, 61m, 2000m);
    private static FoodDefinition FriedChicken =>
        new("SYN-009", "炸鸡", 100, 250m, 18m, 16m, 10m, 700m);
    private static FoodDefinition PickledVegetables =>
        new("SYN-010", "腌菜", 100, 30m, 1.5m, 0.2m, 5m, 3000m);
    private static FoodDefinition SweetDrink =>
        new("SYN-011", "甜饮料", 100, 42m, 0m, 0m, 10.5m, 5m);
    private static FoodDefinition RicePorridge =>
        new("SYN-012", "白粥", 100, 46m, 1.1m, 0.3m, 9.8m, 5m);
    private static FoodDefinition Apple =>
        new("SYN-013", "苹果", 85, 53m, 0.4m, 0.2m, 13.7m, 2m);
    private static FoodDefinition GreenVegetables =>
        new("SYN-014", "清炒青菜", 100, 50m, 2m, 2.5m, 4m, 300m);
    private static FoodDefinition Tofu =>
        new("SYN-015", "豆腐", 100, 84m, 6.6m, 2m, 3.4m, 7m);
    private static FoodDefinition LowFatMilk =>
        new("SYN-016", "低脂牛奶", 100, 54m, 3m, 1.5m, 3.4m, 37m);

    private static ScenarioEntryDefinition Entry(
        FoodDefinition food,
        decimal weight,
        MealOccasion meal,
        bool isAllEdible = true) => new(food, weight, meal, isAllEdible);

    private sealed record DietaryScenarioDefinition(
        string Key,
        string DisplayName,
        bool IncludesMeasurements,
        IReadOnlyList<ScenarioEntryDefinition> EntryDefinitions,
        IReadOnlyDictionary<string, ExpectedNutrient> ExpectedNutrients);

    private sealed record DietaryScenario(
        string Key,
        ConsultationWorkspace Workspace,
        IReadOnlyList<ScenarioEntry> Entries,
        bool IncludesMeasurements,
        IReadOnlyDictionary<string, ExpectedNutrient> ExpectedNutrients);

    private sealed record ScenarioEntryDefinition(
        FoodDefinition Food,
        decimal Weight,
        MealOccasion Meal,
        bool IsAllEdible);

    private sealed record ScenarioEntry(
        string FoodName,
        MealOccasion Meal,
        decimal ExpectedEdibleAmount);

    private sealed record ExpectedNutrient(
        decimal Intake,
        AdviceReferenceComparison Comparison);

    private sealed record FoodDefinition(
        string Code,
        string Name,
        int EdiblePortion,
        decimal Energy,
        decimal Protein,
        decimal Fat,
        decimal Carbohydrate,
        decimal Sodium);

    private sealed class RecordingHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false)
        {
            BaseAddress = BaseAddress
        };
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Assert.Equal(new Uri(BaseAddress, "Prescription/Generate"), request.RequestUri);
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Serialize(new AiResultDto("合成建议", false));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $"data: {result}\n\ndata: [DONE]\n\n",
                    Encoding.UTF8,
                    "text/event-stream")
            };
        }
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
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EER>>([]);

        public Task<IReadOnlyList<DietaryReferenceIntakeValue>> GetDietaryReferenceIntakesAsync(
            NutritionSubjectQuery subject,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DietaryReferenceIntakeValue>>([]);

        public Task<IReadOnlyList<Food>> GetFoodsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Food>>([]);

        public Task<IReadOnlyList<Nutrient>> GetNutrientsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Nutrient>>([]);

        public Task<IReadOnlyList<FoodNutrientValue>> GetFoodCompositionAsync(
            string friendlyCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FoodNutrientValue>>([]);
    }
}
