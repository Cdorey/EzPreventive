using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Client.Models;
using EzNutrition.Client.Models.DietarySurvey;
using EzNutrition.Shared.Data.DietaryRecallSurvey;
using EzNutrition.Shared.Data.DTO.PromptDto;
using EzNutrition.Shared.Data.Entities;
using RuntimeArchive = EzNutrition.Client.Models.Archive;
using RuntimeDietaryRecallSurvey = EzNutrition.Client.Models.DietarySurvey.DietaryRecallSurvey;

namespace EzNutrition.Client.Tests.Fixtures;

/// <summary>
/// 指定合成咨询在当前 WASM 工作流中的完成程度。
/// </summary>
internal enum ConsultationWorkflow
{
    /// <summary>仅建立咨询对象，尚未确认基本信息。</summary>
    PreConfirmation,

    /// <summary>已加载参考资料，尚未执行专业核算。</summary>
    InitializedDraft,

    /// <summary>已完成部分核算和部分病史记录。</summary>
    PartialCalculation,

    /// <summary>已完成核算、病史和 AI 建议。</summary>
    CompleteConsultation,

    /// <summary>包含资料缺失、参考值冲突或少见临床组合的草稿。</summary>
    IrregularDraft
}

/// <summary>
/// 表示一例可重复建立的合成咨询及其预期结构。
/// </summary>
internal sealed record ConsultationScenario
{
    /// <summary>获取场景稳定键。</summary>
    public required string Key { get; init; }

    /// <summary>获取工作流阶段。</summary>
    public required ConsultationWorkflow Workflow { get; init; }

    /// <summary>获取运行态咨询。</summary>
    public required RuntimeArchive Archive { get; init; }

    /// <summary>获取预期膳食条目数。</summary>
    public required int ExpectedDietaryEntryCount { get; init; }

    /// <summary>获取 DRIs 是否包含无法自动聚合的基础值。</summary>
    public required bool ExpectsDriConflict { get; init; }

    /// <summary>获取预期归档的 AI 建议状态。</summary>
    public NutritionAdviceGenerationStatus? ExpectedAdviceStatus { get; init; }
}

/// <summary>
/// 建立覆盖年龄、体型、生理状态和咨询完成度的五十例合成咨询。
/// </summary>
internal static class ConsultationScenarioCatalog
{
    private static readonly IReadOnlyList<ScenarioProfile> Profiles =
    [
        new("child-male", "学龄儿童男", "男", 6, 116m, 20m, "", "生长发育期，日常活动正常"),
        new("adolescent-female", "青春期女性", "女", 14, 158m, 47m, "", "学习压力较大，进餐时间不固定"),
        new("young-male", "青年男性", "男", 22, 181m, 72m, "", "规律运动，无已知慢性病"),
        new("adult-female", "成年女性", "女", 34, 163m, 55m, "", "办公室工作，近期希望改善膳食结构"),
        new("obesity-male", "肥胖成年男性", "男", 46, 170m, 108m, "", "体重明显增加，合并多项代谢风险线索"),
        new("postmenopausal-female", "绝经后女性", "女", 56, 157m, 66m, "已绝经", "关注骨健康及长期体重管理"),
        new("older-male", "老年男性", "男", 69, 166m, 59m, "", "食欲下降，需评估蛋白质和总能量"),
        new("oldest-female", "高龄女性", "女", 88, 149m, 43m, "已绝经", "高龄且膳食种类有限，需要完整复核"),
        new("pregnancy-female", "孕中期女性", "女", 30, 165m, 64m, "孕中期", "孕中期常规营养评估"),
        new("lactation-female", "乳母", "女", 32, 160m, 58m, "乳母", "哺乳期膳食及能量评估")
    ];

    private static readonly ConsultationWorkflow[] Workflows =
    [
        ConsultationWorkflow.PreConfirmation,
        ConsultationWorkflow.InitializedDraft,
        ConsultationWorkflow.PartialCalculation,
        ConsultationWorkflow.CompleteConsultation,
        ConsultationWorkflow.IrregularDraft
    ];

    /// <summary>
    /// 获取全部场景的稳定键。
    /// </summary>
    public static IReadOnlyList<string> Keys { get; } = Profiles
        .SelectMany(profile => Workflows.Select(workflow => Key(profile, workflow)))
        .ToArray();

    /// <summary>
    /// 根据稳定键建立一例全新的合成咨询。
    /// </summary>
    public static async Task<ConsultationScenario> CreateAsync(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var definition = Profiles
            .SelectMany((profile, profileIndex) => Workflows.Select((workflow, workflowIndex) => new
            {
                Profile = profile,
                ProfileIndex = profileIndex,
                Workflow = workflow,
                WorkflowIndex = workflowIndex,
                Key = Key(profile, workflow)
            }))
            .Single(item => string.Equals(item.Key, key, StringComparison.Ordinal));

        var seed = 100 + (definition.ProfileIndex * Workflows.Length) + definition.WorkflowIndex;
        var profile = definition.Profile;
        var workflow = definition.Workflow;
        var name = workflow == ConsultationWorkflow.PreConfirmation && definition.ProfileIndex % 3 == 0
            ? null
            : $"合成·{profile.DisplayName}·{WorkflowCode(workflow)}";
        var physiologicalPeriod = workflow == ConsultationWorkflow.IrregularDraft && profile.Code == "young-male"
            ? "孕晚期"
            : profile.PhysiologicalPeriod;
        decimal? height = workflow == ConsultationWorkflow.IrregularDraft && definition.ProfileIndex % 2 == 0
            ? null
            : profile.Height;
        decimal? weight = workflow == ConsultationWorkflow.IrregularDraft && definition.ProfileIndex % 3 == 0
            ? null
            : profile.Weight;
        var archive = RuntimeArchiveSamples.CreateArchive(
            seed,
            name,
            profile.Gender,
            profile.Age,
            height,
            weight,
            physiologicalPeriod);

        return workflow switch
        {
            ConsultationWorkflow.PreConfirmation => new ConsultationScenario
            {
                Key = key,
                Workflow = workflow,
                Archive = archive,
                ExpectedDietaryEntryCount = 0,
                ExpectsDriConflict = false
            },
            ConsultationWorkflow.InitializedDraft => await CreateInitializedDraftAsync(key, archive, profile, seed),
            ConsultationWorkflow.PartialCalculation => await CreatePartialCalculationAsync(key, archive, profile, seed),
            ConsultationWorkflow.CompleteConsultation => await CreateCompleteConsultationAsync(key, archive, profile, seed),
            ConsultationWorkflow.IrregularDraft => await CreateIrregularDraftAsync(key, archive, profile, seed),
            _ => throw new ArgumentOutOfRangeException(nameof(key))
        };
    }

    private static async Task<ConsultationScenario> CreateInitializedDraftAsync(
        string key,
        RuntimeArchive archive,
        ScenarioProfile profile,
        int seed)
    {
        archive.CurrentEnergyCalculator = CreateEnergyCalculator(archive.Client, seed, calculate: false);
        archive.DRIs = CreateDris(archive.Client, DriPattern.Normal);
        archive.DietaryRecallSurvey = await CreateDietarySurveyAsync(
            archive.Client,
            archive.DRIs,
            seed,
            DietaryPattern.Uncalculated);
        archive.DietaryTower = CreateTower(archive);
        archive.SubjectiveObjectiveAssessmentPlanInformation = new();

        return new ConsultationScenario
        {
            Key = key,
            Workflow = ConsultationWorkflow.InitializedDraft,
            Archive = archive,
            ExpectedDietaryEntryCount = 0,
            ExpectsDriConflict = false
        };
    }

    private static async Task<ConsultationScenario> CreatePartialCalculationAsync(
        string key,
        RuntimeArchive archive,
        ScenarioProfile profile,
        int seed)
    {
        archive.CurrentEnergyCalculator = CreateEnergyCalculator(archive.Client, seed, calculate: true);
        archive.DRIs = CreateDris(archive.Client, DriPattern.Normal);
        archive.DietaryRecallSurvey = await CreateDietarySurveyAsync(
            archive.Client,
            archive.DRIs,
            seed,
            DietaryPattern.SingleEntry);
        archive.DietaryTower = CreateTower(archive);
        archive.SubjectiveObjectiveAssessmentPlanInformation = new SubjectiveObjectiveAssessmentPlanInformation
        {
            Subjective = profile.ClinicalSummary,
            Objective = $"合成测量：身高 {archive.Client.Height?.ToString() ?? "未测"} cm，体重 {archive.Client.Weight?.ToString() ?? "未测"} kg。"
        };

        return new ConsultationScenario
        {
            Key = key,
            Workflow = ConsultationWorkflow.PartialCalculation,
            Archive = archive,
            ExpectedDietaryEntryCount = 1,
            ExpectsDriConflict = false
        };
    }

    private static async Task<ConsultationScenario> CreateCompleteConsultationAsync(
        string key,
        RuntimeArchive archive,
        ScenarioProfile profile,
        int seed)
    {
        var calculator = CreateEnergyCalculator(archive.Client, seed, calculate: true);
        var adoptedEnergy = calculator.Energy.GetValueOrDefault() + 100 + ((seed % 3) * 50);
        if (!calculator.CorrectEnergy(adoptedEnergy))
        {
            throw new InvalidOperationException($"{key} 无法建立专业能量核定值。");
        }

        archive.CurrentEnergyCalculator = calculator;
        archive.DRIs = CreateDris(archive.Client, DriPattern.MixedWithOffset);
        archive.DietaryRecallSurvey = await CreateDietarySurveyAsync(
            archive.Client,
            archive.DRIs,
            seed,
            DietaryPattern.SixMeals);
        archive.DietaryTower = CreateTower(archive);
        archive.SubjectiveObjectiveAssessmentPlanInformation = new SubjectiveObjectiveAssessmentPlanInformation
        {
            Subjective = profile.ClinicalSummary,
            Objective = "合成体格和实验室资料已完成结构化复核。",
            Assessment = "结合能量、DRIs 与全天膳食回顾形成合成营养评估。",
            Plan = "由专业人员复核后形成随访计划。"
        };
        archive.AdvicePrompt = CreatePrompt(archive);
        var requestedAt = archive.ContractIdentity.CreatedAt.AddMinutes(20);
        archive.AiGeneratedAdvice = new AiGeneratedAdvice
        {
            IsReady = true,
            GenerationStatus = AiAdviceGenerationStatus.Completed,
            RequestedAt = requestedAt,
            CompletedAt = requestedAt.AddSeconds(15),
            Environment = new EnvironmentDto("合成生成器", "测试平台", "固定场景"),
            ReasoningContent = $"{key} 的合成分析过程。",
            Content = $"{key} 的合成营养建议。"
        };

        return new ConsultationScenario
        {
            Key = key,
            Workflow = ConsultationWorkflow.CompleteConsultation,
            Archive = archive,
            ExpectedDietaryEntryCount = 6,
            ExpectsDriConflict = false,
            ExpectedAdviceStatus = NutritionAdviceGenerationStatus.Completed
        };
    }

    private static async Task<ConsultationScenario> CreateIrregularDraftAsync(
        string key,
        RuntimeArchive archive,
        ScenarioProfile profile,
        int seed)
    {
        archive.CurrentEnergyCalculator = CreateEnergyCalculator(
            archive.Client,
            seed,
            calculate: true,
            usePopulationAverage: true);
        archive.DRIs = CreateDris(archive.Client, DriPattern.Conflict);
        archive.DietaryRecallSurvey = await CreateDietarySurveyAsync(
            archive.Client,
            archive.DRIs,
            seed,
            DietaryPattern.EmptyCalculated);
        archive.DietaryTower = CreateTower(archive);
        archive.SubjectiveObjectiveAssessmentPlanInformation = new SubjectiveObjectiveAssessmentPlanInformation
        {
            Subjective = profile.ClinicalSummary,
            Objective = "<合成观察值>&\"需要复核\"\n第二行资料尚未完成。",
            Assessment = "资料组合少见，保留原始事实供专业人员判断。"
        };
        archive.AdvicePrompt = CreatePrompt(archive);
        var adviceState = (seed % 4) switch
        {
            0 => NutritionAdviceGenerationStatus.Prepared,
            1 => NutritionAdviceGenerationStatus.Generating,
            2 => NutritionAdviceGenerationStatus.Incomplete,
            _ => NutritionAdviceGenerationStatus.Failed
        };
        archive.AiGeneratedAdvice = CreateAdviceState(adviceState, archive.ContractIdentity.CreatedAt, key);

        return new ConsultationScenario
        {
            Key = key,
            Workflow = ConsultationWorkflow.IrregularDraft,
            Archive = archive,
            ExpectedDietaryEntryCount = 0,
            ExpectsDriConflict = true,
            ExpectedAdviceStatus = adviceState
        };
    }

    private static EnergyCalculator CreateEnergyCalculator(
        IClient client,
        int seed,
        bool calculate,
        bool usePopulationAverage = false)
    {
        var palValues = new[] { 1.4m, 1.5m, 1.7m, 2.0m };
        var pal = palValues[seed % palValues.Length];
        var baseEer = usePopulationAverage
            ? new EER
            {
                Gender = client.Gender,
                AgeStart = client.Age,
                PAL = pal,
                AvgBwEER = 1450 + ((seed % 8) * 120)
            }
            : new EER
            {
                Gender = client.Gender,
                AgeStart = client.Age,
                PAL = pal,
                BEE = 22m + (seed % 6)
            };
        var records = new List<EER> { baseEer };
        var offset = PhysiologicalEnergyOffset(client.SpecialPhysiologicalPeriod);
        if (offset > 0)
        {
            records.Add(new EER
            {
                Gender = client.Gender,
                AgeStart = client.Age,
                SpecialPhysiologicalPeriod = client.SpecialPhysiologicalPeriod,
                OffsetEnergy = offset
            });
        }

        var calculator = new EnergyCalculator(client)
        {
            PAL = pal,
            AvailableEERs = records
        };
        if (calculate && !calculator.Calculate())
        {
            throw new InvalidOperationException("合成场景的能量计算失败。");
        }

        return calculator;
    }

    private static DRIs CreateDris(IClient client, DriPattern pattern)
    {
        var protein = client.Gender == "男" ? 65m : 55m;
        var records = pattern == DriPattern.Conflict
            ? new List<DietaryReferenceIntakeValue>
            {
                Dri(client, "锌", DietaryReferenceIntakeType.RNI, 10m, "mg/d"),
                Dri(client, "锌", DietaryReferenceIntakeType.RNI, 12m, "mg/d")
            }
            : new List<DietaryReferenceIntakeValue>
            {
                Dri(client, "蛋白质", DietaryReferenceIntakeType.RNI, protein, "g/d"),
                Dri(client, "钙", DietaryReferenceIntakeType.RNI, 800m, "mg/d"),
                Dri(client, "钠", DietaryReferenceIntakeType.AI, 1500m, "mg/d"),
                Dri(client, "总脂肪", DietaryReferenceIntakeType.AMDR_L, 20m, "%E"),
                Dri(client, "总脂肪", DietaryReferenceIntakeType.AMDR_H, 30m, "%E"),
                Dri(client, "碳水化合物", DietaryReferenceIntakeType.AMDR_L, 50m, "%E"),
                Dri(client, "碳水化合物", DietaryReferenceIntakeType.AMDR_H, 65m, "%E")
            };
        if (pattern == DriPattern.MixedWithOffset)
        {
            records.Add(Dri(client, "胆碱", DietaryReferenceIntakeType.AI, 400m, "mg/d"));
            if (!string.IsNullOrWhiteSpace(client.SpecialPhysiologicalPeriod))
            {
                records.Add(Dri(
                    client,
                    "钙",
                    DietaryReferenceIntakeType.RNI,
                    200m,
                    "mg/d",
                    isOffset: true,
                    physiologicalPeriod: client.SpecialPhysiologicalPeriod));
            }
        }

        return new DRIs(client) { AvailableDRIs = records };
    }

    private static DietaryReferenceIntakeValue Dri(
        IClient client,
        string nutrient,
        DietaryReferenceIntakeType type,
        decimal value,
        string unit,
        bool isOffset = false,
        string? physiologicalPeriod = null) => new()
        {
            Nutrient = nutrient,
            RecordType = type,
            Value = value,
            MeasureUnit = unit,
            IsOffset = isOffset,
            Gender = client.Gender,
            SpecialPhysiologicalPeriod = physiologicalPeriod,
            AgeStart = AgeGroupStart(client.Age),
            Detail = "合成场景参考记录"
        };

    private static async Task<RuntimeDietaryRecallSurvey> CreateDietarySurveyAsync(
        IClient client,
        DRIs dris,
        int seed,
        DietaryPattern pattern)
    {
        var nutrients = RuntimeArchiveSamples.CreateNutrients();
        var foods = RuntimeArchiveSamples.CreateFoods(nutrients, seed);
        var survey = new RuntimeDietaryRecallSurvey(client, foods, nutrients, dris);
        switch (pattern)
        {
            case DietaryPattern.SingleEntry:
                survey.RecallEntries.Add(Entry(
                    seed,
                    1,
                    foods[seed % foods.Count],
                    80m + (seed % 5 * 20m),
                    MealOccasion.Breakfast,
                    seed % 2 == 0));
                break;
            case DietaryPattern.SixMeals:
                survey.RecallEntries.AddRange(
                [
                    Entry(seed, 1, foods[0], 80m, MealOccasion.Breakfast, true),
                    Entry(seed, 2, foods[2], 120m, MealOccasion.MorningSnack, true),
                    Entry(seed, 3, foods[1], 150m, MealOccasion.Lunch, false),
                    Entry(seed, 4, foods[0], 60m, MealOccasion.AfternoonSnack, true),
                    Entry(seed, 5, foods[2], 200m, MealOccasion.Dinner, true),
                    Entry(seed, 6, foods[1], 50m, MealOccasion.LateNightSnack, false)
                ]);
                break;
        }

        if (pattern != DietaryPattern.Uncalculated)
        {
            await survey.CalculateAsync();
        }

        return survey;
    }

    private static DietaryRecallEntry Entry(
        int seed,
        int sequence,
        Food food,
        decimal weight,
        MealOccasion meal,
        bool allEdible) => new()
        {
            EntryId = RuntimeArchiveSamples.StableGuid(0x61000000, seed, sequence),
            Food = food,
            Weight = weight,
            MealOccasion = meal,
            IsAllEdible = allEdible
        };

    private static DietaryRecallTower? CreateTower(RuntimeArchive archive)
    {
        if (archive.DietaryRecallSurvey is null || StandardTower.GetStandardTower(archive.Client.Age) is not { } standard)
        {
            return null;
        }

        return new DietaryRecallTower(archive.DietaryRecallSurvey.RecallEntries, standard);
    }

    private static PromptDto CreatePrompt(RuntimeArchive archive)
    {
        var soap = archive.SubjectiveObjectiveAssessmentPlanInformation ?? new();
        return new PromptDto
        {
            PatientInfo = new PatientInfo
            {
                Gender = archive.Client.Gender,
                Age = archive.Client.Age,
                BMI = archive.CurrentEnergyCalculator?.BMI,
                PAL = archive.CurrentEnergyCalculator?.PAL,
                Height = archive.Client.Height,
                Weight = archive.Client.Weight,
                TotalBalanceEnergyViaCalculation = archive.CurrentEnergyCalculator?.Energy,
                SpecialPhysiologicalPeriod = archive.Client.SpecialPhysiologicalPeriod
            },
            DietaryRecallSurvey = archive.DietaryRecallSurvey?.SummaryRows.Count > 0
                ? new EzNutrition.Shared.Data.DTO.PromptDto.DietaryRecallSurvey
                {
                    DeficientNutrients = archive.DietaryRecallSurvey.SummaryRows
                        .Where(row => row.Flag == Flags.Lower)
                        .Select(row => row.FriendlyName)
                        .ToArray(),
                    ExcessiveNutrients = archive.DietaryRecallSurvey.SummaryRows
                        .Where(row => row.Flag == Flags.Higher)
                        .Select(row => row.FriendlyName)
                        .ToArray()
                }
                : null,
            ClinicalInfo = new ClinicalInfo
            {
                Subjective = soap.Subjective,
                Objective = soap.Objective,
                Assessment = soap.Assessment,
                Plan = soap.Plan
            }
        };
    }

    private static AiGeneratedAdvice CreateAdviceState(
        NutritionAdviceGenerationStatus status,
        DateTimeOffset createdAt,
        string key)
    {
        var requestedAt = createdAt.AddMinutes(10);
        return status switch
        {
            NutritionAdviceGenerationStatus.Prepared => new AiGeneratedAdvice
            {
                GenerationStatus = AiAdviceGenerationStatus.Prepared
            },
            NutritionAdviceGenerationStatus.Generating => new AiGeneratedAdvice
            {
                Sending = true,
                GenerationStatus = AiAdviceGenerationStatus.Generating,
                RequestedAt = requestedAt,
                ReasoningContent = $"{key} 正在生成。"
            },
            NutritionAdviceGenerationStatus.Incomplete => new AiGeneratedAdvice
            {
                GenerationStatus = AiAdviceGenerationStatus.Incomplete,
                RequestedAt = requestedAt,
                CompletedAt = requestedAt.AddSeconds(5),
                Content = $"{key} 的不完整草稿。"
            },
            NutritionAdviceGenerationStatus.Failed => new AiGeneratedAdvice
            {
                GenerationStatus = AiAdviceGenerationStatus.Failed,
                RequestedAt = requestedAt,
                CompletedAt = requestedAt.AddSeconds(3)
            },
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
    }

    private static decimal AgeGroupStart(int age) => age switch
    {
        < 7 => 4m,
        < 11 => 7m,
        < 14 => 11m,
        < 18 => 14m,
        < 50 => 18m,
        < 65 => 50m,
        < 80 => 65m,
        _ => 80m
    };

    private static decimal PhysiologicalEnergyOffset(string? physiologicalPeriod) => physiologicalPeriod switch
    {
        "孕早期" => 50m,
        "孕中期" => 300m,
        "孕晚期" => 450m,
        "乳母" => 500m,
        _ => 0m
    };

    private static string Key(ScenarioProfile profile, ConsultationWorkflow workflow) =>
        $"{profile.Code}-{WorkflowCode(workflow)}";

    private static string WorkflowCode(ConsultationWorkflow workflow) => workflow switch
    {
        ConsultationWorkflow.PreConfirmation => "pre-confirmation",
        ConsultationWorkflow.InitializedDraft => "initialized-draft",
        ConsultationWorkflow.PartialCalculation => "partial-calculation",
        ConsultationWorkflow.CompleteConsultation => "complete-consultation",
        ConsultationWorkflow.IrregularDraft => "irregular-draft",
        _ => throw new ArgumentOutOfRangeException(nameof(workflow))
    };

    private enum DriPattern
    {
        Normal,
        MixedWithOffset,
        Conflict
    }

    private enum DietaryPattern
    {
        Uncalculated,
        EmptyCalculated,
        SingleEntry,
        SixMeals
    }

    private sealed record ScenarioProfile(
        string Code,
        string DisplayName,
        string Gender,
        int Age,
        decimal Height,
        decimal Weight,
        string PhysiologicalPeriod,
        string ClinicalSummary);
}
