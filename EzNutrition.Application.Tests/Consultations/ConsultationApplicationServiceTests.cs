using EzNutrition.Application.Consultations;
using EzNutrition.Application.Ports;
using EzNutrition.Domain.Assessments;
using EzNutrition.Domain.Consultations;
using EzNutrition.Domain.Dietary;
using EzNutrition.Shared.Data.Entities;

namespace EzNutrition.Application.Tests.Consultations;

/// <summary>
/// 验证咨询应用服务在数据读取、失败和运行态更新之间保持清晰边界。
/// </summary>
public sealed class ConsultationApplicationServiceTests
{
    /// <summary>
    /// 验证初始化仅在全部参考数据可用后更新工作区。
    /// </summary>
    [Fact]
    public async Task InitializeAsync_commits_complete_workspace_after_all_sources_succeed()
    {
        var source = StubNutritionDataSource.CreateValid();
        var service = CreateService(source);
        var client = CreateClient();
        var workspace = new ConsultationWorkspace(client);

        await service.InitializeAsync(workspace);

        Assert.False(workspace.ClientInfoFormEnabled);
        Assert.NotNull(workspace.CurrentEnergyCalculator);
        Assert.NotNull(workspace.DRIs);
        Assert.Single(workspace.DRIs.AvailableDRIs);
        Assert.NotNull(workspace.DietaryRecallSurvey);
        Assert.Single(workspace.DietaryRecallSurvey.Foods);
        Assert.Single(workspace.DietaryRecallSurvey.Nutrients);
        Assert.NotNull(workspace.SubjectiveObjectiveAssessmentPlanInformation);
        Assert.Equal("female", source.LastDriQuery?.Gender);
        Assert.Equal(35, source.LastDriQuery?.Age);
    }

    /// <summary>
    /// 验证任一数据源失败时不会把半成品写入工作区。
    /// </summary>
    [Fact]
    public async Task InitializeAsync_leaves_workspace_unchanged_when_a_source_fails()
    {
        var source = StubNutritionDataSource.CreateValid();
        source.FoodsException = new NutritionDataAccessException("合成目录失败。");
        var service = CreateService(source);
        var workspace = new ConsultationWorkspace(CreateClient());

        await Assert.ThrowsAsync<NutritionDataAccessException>(() => service.InitializeAsync(workspace));

        Assert.True(workspace.ClientInfoFormEnabled);
        Assert.Null(workspace.CurrentEnergyCalculator);
        Assert.Null(workspace.DRIs);
        Assert.Null(workspace.DietaryRecallSurvey);
        Assert.Null(workspace.DietaryTower);
        Assert.Null(workspace.SubjectiveObjectiveAssessmentPlanInformation);
    }

    /// <summary>
    /// 验证能量参考记录由应用服务装载，领域计算器本身不执行 I/O。
    /// </summary>
    [Fact]
    public async Task LoadEnergyReferencesAsync_replaces_calculator_records()
    {
        var source = StubNutritionDataSource.CreateValid();
        var service = CreateService(source);
        var calculator = new EnergyCalculator(CreateClient())
        {
            AvailableEERs = [new EER { EERId = 999 }]
        };

        await service.LoadEnergyReferencesAsync(calculator);

        var record = Assert.Single(calculator.AvailableEERs);
        Assert.Equal(1, record.EERId);
        Assert.Equal("female", source.LastEnergyQuery?.Gender);
    }

    /// <summary>
    /// 验证空食物成分响应会安全失败且不写入空明细。
    /// </summary>
    [Fact]
    public async Task LoadFoodCompositionAsync_rejects_empty_response()
    {
        var source = StubNutritionDataSource.CreateValid();
        source.FoodComposition = [];
        var service = CreateService(source);
        var food = new Food
        {
            FoodId = Guid.NewGuid(),
            FriendlyCode = "01-001",
            FriendlyName = "合成食物"
        };

        var exception = await Assert.ThrowsAsync<NutritionDataAccessException>(
            () => service.LoadFoodCompositionAsync(food));

        Assert.Equal("没有找到该食物的营养成分数据。", exception.Message);
        Assert.Null(food.FoodNutrientValues);
    }

    /// <summary>
    /// 验证后台调度不会改变既有重量折算、营养素合计和供能比算法。
    /// </summary>
    [Fact]
    public async Task CalculateDietaryRecallAsync_preserves_nutrition_results()
    {
        var source = StubNutritionDataSource.CreateValid();
        var service = CreateService(source);
        var client = CreateClient();
        var nutrients = CreateCalculationNutrients();
        var food = CreateCalculationFood(nutrients);
        var dris = new DRIs(client)
        {
            AvailableDRIs =
            [
                Dri("蛋白质", DietaryReferenceIntakeType.RNI, 60m, "g/d"),
                Dri("蛋白质", DietaryReferenceIntakeType.UL, 120m, "g/d"),
                Dri("蛋白质", DietaryReferenceIntakeType.AMDR_L, 10m, "%E"),
                Dri("蛋白质", DietaryReferenceIntakeType.AMDR_H, 20m, "%E")
            ]
        };
        var survey = new DietaryRecallSurvey(client, [food], nutrients, dris);
        survey.RecallEntries.Add(new DietaryRecallEntry
        {
            Food = food,
            Weight = 100m,
            MealOccasion = MealOccasion.Lunch,
            IsAllEdible = true
        });

        await service.CalculateDietaryRecallAsync(survey);

        Assert.Equal(200m, survey.SummaryCalculationTable?.TotalEnergy);
        Assert.Equal(10m, Assessment(survey, "蛋白质").Value);
        Assert.Equal(5m, Assessment(survey, "总脂肪").Value);
        Assert.Equal(20m, Assessment(survey, "碳水化合物").Value);
        Assert.Equal(20m, Assessment(survey, "蛋白质供能比").Value);
        Assert.Equal(22m, Assessment(survey, "脂肪供能比").Value);
        Assert.Equal(40m, Assessment(survey, "碳水化合物供能比").Value);
        var protein = Assessment(survey, "蛋白质");
        Assert.Equal(DietaryReferenceIntakeType.RNI, protein.LowerReference?.Type);
        Assert.Equal(60m, protein.LowerReference?.Value);
        Assert.Equal("g/d", protein.LowerReference?.Unit);
        Assert.Equal(DietaryReferenceIntakeType.UL, protein.UpperReference?.Type);
        var proteinRatio = Assessment(survey, "蛋白质供能比");
        Assert.Equal(DietaryReferenceIntakeType.AMDR_L, proteinRatio.LowerReference?.Type);
        Assert.Equal(DietaryReferenceIntakeType.AMDR_H, proteinRatio.UpperReference?.Type);
        var detail = Assert.Single(survey.EntryCalculations);
        Assert.Equal(200m, detail.NutrientValues[Nutrient(nutrients, "能量").NutrientId]);
        Assert.Equal(100m, detail.EdibleWeight);
        Assert.Equal(MealOccasion.Lunch, detail.MealOccasion);
        Assert.Equal(100m, Assert.Single(survey.RecallEntries).Weight);
    }

    /// <summary>
    /// 验证预先取消的后台核算不会覆盖调查对象已有状态。
    /// </summary>
    [Fact]
    public async Task CalculateDietaryRecallAsync_does_not_apply_a_cancelled_calculation()
    {
        var source = StubNutritionDataSource.CreateValid();
        var service = CreateService(source);
        var client = CreateClient();
        var nutrients = CreateCalculationNutrients();
        var food = CreateCalculationFood(nutrients);
        var survey = new DietaryRecallSurvey(client, [food], nutrients, new DRIs(client));
        survey.RecallEntries.Add(new DietaryRecallEntry { Food = food, Weight = 100m });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.CalculateDietaryRecallAsync(survey, cancellation.Token));

        Assert.Null(survey.SummaryCalculationTable);
        Assert.Empty(survey.NutrientAssessments);
        Assert.Empty(survey.EntryCalculations);
    }

    private static ConsultationApplicationService CreateService(StubNutritionDataSource source) =>
        new(source, source, source);

    private static ClientInfo CreateClient() => new()
    {
        Gender = "female",
        Age = 35,
        Height = 165,
        Weight = 60,
        SpecialPhysiologicalPeriod = string.Empty
    };

    private static DietaryNutrientAssessment Assessment(
        DietaryRecallSurvey survey,
        string name) => Assert.Single(
            survey.NutrientAssessments,
            assessment => assessment.FriendlyName == name);

    private static Nutrient Nutrient(IEnumerable<Nutrient> nutrients, string name) =>
        Assert.Single(nutrients, nutrient => nutrient.FriendlyName == name);

    private static Nutrient[] CreateCalculationNutrients()
    {
        var names = new[]
        {
            "能量", "蛋白质", "脂肪", "碳水化合物", "钾", "钠", "镁", "铁", "锰", "锌", "磷", "硒", "铜",
            "总维生素A", "视黄醇", "胡萝卜素", "硫胺素", "核黄素", "烟酸", "维生素C", "总维生素E"
        };
        return names.Select((name, index) => new Nutrient
        {
            NutrientId = index + 1,
            FriendlyName = name,
            DefaultMeasureUnit = name == "能量" ? "kCal" : "g"
        }).ToArray();
    }

    private static Food CreateCalculationFood(IReadOnlyList<Nutrient> nutrients)
    {
        var food = new Food
        {
            FoodId = Guid.NewGuid(),
            FriendlyCode = "TEST-ASYNC",
            FriendlyName = "后台核算测试食物",
            EdiblePortion = 100
        };
        var values = new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["能量"] = 200m,
            ["蛋白质"] = 10m,
            ["脂肪"] = 5m,
            ["碳水化合物"] = 20m
        };
        food.FoodNutrientValues = nutrients.Select(nutrient => new FoodNutrientValue
        {
            Food = food,
            FoodId = food.FoodId,
            Nutrient = nutrient,
            NutrientId = nutrient.NutrientId,
            MeasureUnit = nutrient.DefaultMeasureUnit,
            Value = values.GetValueOrDefault(nutrient.FriendlyName ?? string.Empty, 1m)
        }).ToList();
        return food;
    }

    private static DietaryReferenceIntakeValue Dri(
        string nutrient,
        DietaryReferenceIntakeType type,
        decimal value,
        string unit) => new()
        {
            Nutrient = nutrient,
            RecordType = type,
            Value = value,
            MeasureUnit = unit
        };

    private sealed class StubNutritionDataSource :
        IEnergyReferenceDataSource,
        IDietaryReferenceIntakeDataSource,
        IFoodCompositionDataSource
    {
        public required IReadOnlyList<EER> EnergyReferences { get; init; }

        public required IReadOnlyList<DietaryReferenceIntakeValue> DietaryReferenceIntakes { get; init; }

        public required IReadOnlyList<Food> Foods { get; init; }

        public required IReadOnlyList<Nutrient> Nutrients { get; init; }

        public required IReadOnlyList<FoodNutrientValue> FoodComposition { get; set; }

        public Exception? FoodsException { get; set; }

        public NutritionSubjectQuery? LastEnergyQuery { get; private set; }

        public NutritionSubjectQuery? LastDriQuery { get; private set; }

        public Task<IReadOnlyList<EER>> GetEnergyReferencesAsync(
            NutritionSubjectQuery subject,
            CancellationToken cancellationToken = default)
        {
            LastEnergyQuery = subject;
            return Task.FromResult(EnergyReferences);
        }

        public Task<IReadOnlyList<DietaryReferenceIntakeValue>> GetDietaryReferenceIntakesAsync(
            NutritionSubjectQuery subject,
            CancellationToken cancellationToken = default)
        {
            LastDriQuery = subject;
            return Task.FromResult(DietaryReferenceIntakes);
        }

        public Task<IReadOnlyList<Food>> GetFoodsAsync(CancellationToken cancellationToken = default) =>
            FoodsException is null
                ? Task.FromResult(Foods)
                : Task.FromException<IReadOnlyList<Food>>(FoodsException);

        public Task<IReadOnlyList<Nutrient>> GetNutrientsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Nutrients);

        public Task<IReadOnlyList<FoodNutrientValue>> GetFoodCompositionAsync(
            string friendlyCode,
            CancellationToken cancellationToken = default) => Task.FromResult(FoodComposition);

        public static StubNutritionDataSource CreateValid()
        {
            var nutrient = new Nutrient
            {
                NutrientId = 1,
                FriendlyName = "能量",
                DefaultMeasureUnit = "kcal"
            };
            var food = new Food
            {
                FoodId = Guid.NewGuid(),
                FriendlyCode = "01-001",
                FriendlyName = "合成食物"
            };
            return new StubNutritionDataSource
            {
                EnergyReferences =
                [
                    new EER
                    {
                        EERId = 1,
                        Gender = "female",
                        AgeStart = 18,
                        PAL = 1.5m,
                        BEE = 21m
                    }
                ],
                DietaryReferenceIntakes =
                [
                    new DietaryReferenceIntakeValue
                    {
                        DietaryReferenceIntakeValueId = 1,
                        Gender = "female",
                        AgeStart = 18,
                        Nutrient = "蛋白质",
                        RecordType = DietaryReferenceIntakeType.RNI,
                        Value = 65,
                        MeasureUnit = "g/d"
                    }
                ],
                Foods = [food],
                Nutrients = [nutrient],
                FoodComposition =
                [
                    new FoodNutrientValue
                    {
                        FoodId = food.FoodId,
                        Food = food,
                        NutrientId = nutrient.NutrientId,
                        Nutrient = nutrient,
                        Value = 100,
                        MeasureUnit = "kcal"
                    }
                ]
            };
        }
    }
}
