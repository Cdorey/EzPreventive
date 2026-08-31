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
            var standardTower = StandardTower.GetStandardTower(ReferenceAgeInYears(workspace.Client));
            workspace.DietaryTower = standardTower is null
                ? null
                : new DietaryRecallTower(dietaryRecallSurvey.RecallEntries, standardTower);
        };

        workspace.CurrentEnergyCalculator = new EnergyCalculator(workspace.Client);
        workspace.DRIs = dris;
        workspace.DietaryRecallSurvey = dietaryRecallSurvey;
        workspace.DietaryTower = StandardTower.GetStandardTower(subject.AgeInYears);
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

    /// <summary>
    /// 在托管线程池上核算一份稳定的膳食记录快照，并在输入未变化时应用结果。
    /// </summary>
    /// <param name="survey">待核算的膳食回顾调查。</param>
    /// <param name="cancellationToken">用于放弃尚未开始的核算或阻止应用结果的取消标记。</param>
    public async Task CalculateDietaryRecallAsync(
        DietaryRecallSurvey survey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(survey);

        // 食物目录、成分表和 DRIs 在咨询初始化后保持只读；这里只复制医生仍可编辑的记录字段。
        var entries = survey.RecallEntries.Select(CreateEntrySnapshot).ToArray();
        var result = await Task.Run(
            () => survey.CreateCalculation(entries),
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        if (!MatchesCurrentEntries(survey.RecallEntries, entries))
        {
            throw new InvalidOperationException("膳食记录在核算期间发生变化，请重新计算。");
        }

        survey.ApplyCalculation(result);
    }

    /// <summary>
    /// 将专业人员已经复核的候选文本追加到当前 SOAP 记录的对应部分。
    /// </summary>
    /// <returns>实际追加了至少一段有效文本时返回 <see langword="true" />。</returns>
    public bool AppendSoapContribution(
        ConsultationWorkspace workspace,
        SoapContribution contribution)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(contribution);

        var information = GetSoapInformation(workspace);
        var subjective = AppendConfirmedText(information.Subjective, contribution.Subjective);
        var objective = AppendConfirmedText(information.Objective, contribution.Objective);
        var assessment = AppendConfirmedText(information.Assessment, contribution.Assessment);
        var plan = AppendConfirmedText(information.Plan, contribution.Plan);
        var changed = !string.Equals(subjective, information.Subjective, StringComparison.Ordinal)
            || !string.Equals(objective, information.Objective, StringComparison.Ordinal)
            || !string.Equals(assessment, information.Assessment, StringComparison.Ordinal)
            || !string.Equals(plan, information.Plan, StringComparison.Ordinal);

        if (changed)
        {
            information.Subjective = subjective;
            information.Objective = objective;
            information.Assessment = assessment;
            information.Plan = plan;
        }

        return changed;
    }

    private static NutritionSubjectQuery CreateSubjectQuery(IClient client)
    {
        if (string.IsNullOrWhiteSpace(client.Gender) || client.Age is null)
        {
            throw new InvalidOperationException("性别和年龄信息无效。");
        }

        return new NutritionSubjectQuery
        {
            Gender = client.Gender.Trim(),
            AgeInYears = ReferenceAgeInYears(client),
            SpecialPhysiologicalPeriod = client.SpecialPhysiologicalPeriod ?? string.Empty
        };
    }

    private static decimal ReferenceAgeInYears(IClient client) =>
        client.Age?.ToReferenceYears()
        ?? throw new InvalidOperationException("年龄信息无效。");

    private static DietaryRecallEntry CreateEntrySnapshot(DietaryRecallEntry entry) => new()
    {
        EntryId = entry.EntryId,
        Food = entry.Food,
        Weight = entry.Weight,
        MealOccasion = entry.MealOccasion,
        IsAllEdible = entry.IsAllEdible
    };

    private static bool MatchesCurrentEntries(
        IReadOnlyList<DietaryRecallEntry> current,
        IReadOnlyList<DietaryRecallEntry> snapshot)
    {
        if (current.Count != snapshot.Count)
        {
            return false;
        }

        for (var index = 0; index < current.Count; index++)
        {
            var currentEntry = current[index];
            var snapshotEntry = snapshot[index];
            if (currentEntry.EntryId != snapshotEntry.EntryId ||
                currentEntry.Food.FoodId != snapshotEntry.Food.FoodId ||
                currentEntry.Weight != snapshotEntry.Weight ||
                currentEntry.MealOccasion != snapshotEntry.MealOccasion ||
                currentEntry.IsAllEdible != snapshotEntry.IsAllEdible)
            {
                return false;
            }
        }

        return true;
    }

    private static string AppendConfirmedText(string current, string? addition)
    {
        if (string.IsNullOrWhiteSpace(addition))
        {
            return current;
        }

        if (current.Length == 0
            || current.EndsWith('\r')
            || current.EndsWith('\n')
            || addition.StartsWith('\r')
            || addition.StartsWith('\n'))
        {
            return current + addition;
        }

        return current + "\n" + addition;
    }

    private static SubjectiveObjectiveAssessmentPlanInformation GetSoapInformation(
        ConsultationWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        return workspace.SubjectiveObjectiveAssessmentPlanInformation
            ?? throw new InvalidOperationException("SOAP 记录尚未初始化。");
    }

    private static void EnsureNotEmpty<T>(IReadOnlyCollection<T>? values, string message)
    {
        if (values is null || values.Count == 0)
        {
            throw new NutritionDataAccessException(
                message,
                NutritionDataAccessFailureKind.NotFound);
        }
    }
}
