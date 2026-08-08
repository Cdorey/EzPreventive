using EzNutrition.Application.Ports;
using EzNutrition.Domain.Assessments;
using EzNutrition.Domain.Consultations;
using EzNutrition.Domain.Dietary;
using EzNutrition.Shared.Data.Entities;

namespace EzNutrition.Application.Consultations;

/// <summary>
/// 编排一次营养咨询工作区所需的数据读取和运行态对象建立过程。
/// </summary>
public sealed class ConsultationApplicationService(
    IEnergyReferenceDataSource energyReferenceDataSource,
    IDietaryReferenceIntakeDataSource dietaryReferenceIntakeDataSource,
    IFoodCompositionDataSource foodCompositionDataSource)
{
    /// <summary>
    /// 读取 DRIs 与食物成分目录，并在全部数据可用后一次性初始化工作区。
    /// </summary>
    public async Task InitializeAsync(
        ConsultationWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var subject = CreateSubjectQuery(workspace.Client);

        var drisTask = dietaryReferenceIntakeDataSource.GetDietaryReferenceIntakesAsync(
            subject,
            cancellationToken);
        var foodsTask = foodCompositionDataSource.GetFoodsAsync(cancellationToken);
        var nutrientsTask = foodCompositionDataSource.GetNutrientsAsync(cancellationToken);

        await Task.WhenAll(drisTask, foodsTask, nutrientsTask);
        cancellationToken.ThrowIfCancellationRequested();

        var driRecords = await drisTask;
        var foods = await foodsTask;
        var nutrients = await nutrientsTask;
        EnsureNotEmpty(driRecords, "服务器没有返回可用的膳食参考摄入量记录。");
        EnsureNotEmpty(foods, "服务器没有返回可用的食物目录。");
        EnsureNotEmpty(nutrients, "服务器没有返回可用的营养素目录。");

        var dris = new DRIs(workspace.Client)
        {
            AvailableDRIs = [.. driRecords]
        };
        var dietaryRecallSurvey = new DietaryRecallSurvey(
            workspace.Client,
            foods,
            nutrients,
            dris);
        dietaryRecallSurvey.OnCalculate += (_, _) =>
        {
            var standardTower = StandardTower.GetStandardTower(workspace.Client.Age);
            workspace.DietaryTower = standardTower is null
                ? null
                : new DietaryRecallTower(dietaryRecallSurvey.RecallEntries, standardTower);
        };

        workspace.CurrentEnergyCalculator = new EnergyCalculator(workspace.Client);
        workspace.DRIs = dris;
        workspace.DietaryRecallSurvey = dietaryRecallSurvey;
        workspace.DietaryTower = StandardTower.GetStandardTower(workspace.Client.Age);
        workspace.SubjectiveObjectiveAssessmentPlanInformation = new();
        workspace.ClientInfoFormEnabled = false;
    }

    /// <summary>
    /// 为能量计算器读取并替换当前咨询对象适用的 EER 记录。
    /// </summary>
    public async Task LoadEnergyReferencesAsync(
        EnergyCalculator calculator,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(calculator);
        var records = await energyReferenceDataSource.GetEnergyReferencesAsync(
            CreateSubjectQuery(calculator.Client),
            cancellationToken);
        EnsureNotEmpty(records, "服务器没有返回可用的能量参考记录。");
        calculator.AvailableEERs = [.. records];
    }

    /// <summary>
    /// 为 DRIs 评估读取并替换当前咨询对象适用的参考摄入量记录。
    /// </summary>
    public async Task LoadDietaryReferenceIntakesAsync(
        DRIs dris,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dris);
        var records = await dietaryReferenceIntakeDataSource.GetDietaryReferenceIntakesAsync(
            CreateSubjectQuery(dris.Client),
            cancellationToken);
        EnsureNotEmpty(records, "服务器没有返回可用的膳食参考摄入量记录。");
        dris.AvailableDRIs = [.. records];
    }

    /// <summary>
    /// 为指定食物读取并替换营养成分明细。
    /// </summary>
    public async Task LoadFoodCompositionAsync(
        Food food,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(food);
        if (string.IsNullOrWhiteSpace(food.FriendlyCode))
        {
            throw new InvalidOperationException("食物缺少可用于读取成分数据的代码。");
        }

        var values = await foodCompositionDataSource.GetFoodCompositionAsync(
            food.FriendlyCode,
            cancellationToken);
        EnsureNotEmpty(values, "没有找到该食物的营养成分数据。");
        food.FoodNutrientValues = [.. values];
    }

    private static NutritionSubjectQuery CreateSubjectQuery(IClient client)
    {
        if (string.IsNullOrWhiteSpace(client.Gender) || client.Age < 0)
        {
            throw new InvalidOperationException("性别和年龄信息无效。");
        }

        return new NutritionSubjectQuery
        {
            Gender = client.Gender.Trim(),
            Age = client.Age,
            SpecialPhysiologicalPeriod = client.SpecialPhysiologicalPeriod ?? string.Empty
        };
    }

    private static void EnsureNotEmpty<T>(IReadOnlyCollection<T>? values, string message)
    {
        if (values is null || values.Count == 0)
        {
            throw new NutritionDataAccessException(message);
        }
    }
}
