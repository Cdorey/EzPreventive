using EzNutrition.Application.Consultations;
using EzNutrition.Application.Ports;
using EzNutrition.Domain.Assessments;
using EzNutrition.Domain.Consultations;
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
