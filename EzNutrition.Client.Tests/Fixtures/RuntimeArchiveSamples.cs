using EzNutrition.Application.Archives;
using EzNutrition.Application.Consultations;
using EzNutrition.Domain.Assessments;
using EzNutrition.Domain.Consultations;
using EzNutrition.Domain.Dietary;
using EzNutrition.Shared.Data.DTO.PromptDto;
using EzNutrition.Shared.Data.Entities;
using RuntimeArchive = EzNutrition.Application.Consultations.ConsultationWorkspace;
using RuntimeDietaryRecallSurvey = EzNutrition.Domain.Dietary.DietaryRecallSurvey;

namespace EzNutrition.Client.Tests.Fixtures;

/// <summary>
/// 表示一份完全由合成数据建立的 WASM 运行态咨询样本。
/// </summary>
/// <param name="Key">样本稳定键。</param>
/// <param name="Archive">运行态咨询。</param>
internal sealed record RuntimeArchiveSample(string Key, RuntimeArchive Archive);

/// <summary>
/// 提供覆盖当前 WASM 计算路径的合成咨询样本。
/// </summary>
internal static class RuntimeArchiveSamples
{
    private static readonly DateTimeOffset BaseTime = new(2026, 6, 1, 8, 0, 0, TimeSpan.FromHours(8));

    /// <summary>
    /// 建立全部运行态咨询样本。
    /// </summary>
    public static async Task<IReadOnlyList<RuntimeArchiveSample>> CreateAllAsync()
    {
        var samples = new List<RuntimeArchiveSample>
        {
            CreateMinimalDraft(),
            CreateAdultAutomaticEnergy(),
            CreateAdultManualEnergy(),
            CreatePregnancyDri(),
            CreateChildPopulationEnergy(),
            await CreateOlderAdultDietaryRecallAsync(),
            CreateDriConflict(),
            CreateUnusualPhysiologyWithAdvice()
        };

        return samples;
    }

    public static RuntimeArchiveSample CreateMinimalDraft()
    {
        var archive = CreateArchive(1, null, null, 25, null, null, string.Empty);
        return new RuntimeArchiveSample("minimal-draft", archive);
    }

    public static RuntimeArchiveSample CreateAdultAutomaticEnergy()
    {
        var archive = CreateArchive(2, "合成成人甲", "男", 35, 170, 70, string.Empty);
        archive.CurrentEnergyCalculator = CreateAutomaticEnergyCalculator(archive.Client, 1.5m, 25m);
        return new RuntimeArchiveSample("adult-automatic-energy", archive);
    }

    public static RuntimeArchiveSample CreateAdultManualEnergy()
    {
        var archive = CreateArchive(3, "合成成人乙", "女", 42, 162, 61, string.Empty);
        var calculator = CreateAutomaticEnergyCalculator(archive.Client, 1.4m, 24m);
        if (!calculator.CorrectEnergy(2100))
        {
            throw new InvalidOperationException("合成样本的专业能量核定失败。");
        }

        archive.CurrentEnergyCalculator = calculator;
        return new RuntimeArchiveSample("adult-manual-energy", archive);
    }

    public static RuntimeArchiveSample CreatePregnancyDri()
    {
        var archive = CreateArchive(4, "合成孕期对象", "女", 30, 165, 63, "孕中期");
        archive.DRIs = new DRIs(archive.Client)
        {
            AvailableDRIs =
            [
                Dri("钙", DietaryReferenceIntakeType.RNI, 800, "mg/d", false, "女", null),
                Dri("钙", DietaryReferenceIntakeType.RNI, 200, "mg/d", true, "女", "孕中期"),
                Dri("铁", DietaryReferenceIntakeType.RNI, 24, "mg/d", false, "女", "孕中期"),
                Dri("铁", DietaryReferenceIntakeType.UL, 42, "mg/d", false, "女", null),
                Dri("胆碱", DietaryReferenceIntakeType.AI, 450, "mg/d", false, "女", "孕中期")
            ]
        };

        return new RuntimeArchiveSample("pregnancy-dri-offset", archive);
    }

    public static RuntimeArchiveSample CreateChildPopulationEnergy()
    {
        var archive = CreateArchive(5, "合成儿童", "女", 8, null, 28, string.Empty);
        var calculator = new EnergyCalculator(archive.Client)
        {
            PAL = 1.4m,
            AvailableEERs =
            [
                new EER
                {
                    Gender = "女",
                    AgeStart = 8,
                    PAL = 1.4m,
                    AvgBwEER = 1600
                }
            ]
        };
        if (!calculator.Calculate())
        {
            throw new InvalidOperationException("合成样本的人群平均能量计算失败。");
        }

        archive.CurrentEnergyCalculator = calculator;
        return new RuntimeArchiveSample("child-population-energy", archive);
    }

    public static async Task<RuntimeArchiveSample> CreateOlderAdultDietaryRecallAsync()
    {
        var archive = CreateArchive(6, "合成长者", "男", 68, 168, 66, string.Empty);
        var dris = new DRIs(archive.Client)
        {
            AvailableDRIs =
            [
                Dri("蛋白质", DietaryReferenceIntakeType.RNI, 65, "g/d", false, "男", null),
                Dri("总脂肪", DietaryReferenceIntakeType.AMDR_L, 20, "%E", false, "男", null),
                Dri("总脂肪", DietaryReferenceIntakeType.AMDR_H, 30, "%E", false, "男", null),
                Dri("碳水化合物", DietaryReferenceIntakeType.AMDR_L, 50, "%E", false, "男", null),
                Dri("碳水化合物", DietaryReferenceIntakeType.AMDR_H, 65, "%E", false, "男", null)
            ]
        };
        var survey = await CreateDietaryRecallAsync(archive.Client, dris, 6);

        archive.DRIs = dris;
        archive.DietaryRecallSurvey = survey;
        archive.DietaryTower = new DietaryRecallTower(
            survey.RecallEntries,
            StandardTower.GetStandardTower(archive.Client.Age)
                ?? throw new InvalidOperationException("没有找到合成样本所需的膳食宝塔。"));
        archive.SubjectiveObjectiveAssessmentPlanInformation = new SubjectiveObjectiveAssessmentPlanInformation
        {
            Subjective = "近一日饮食记录完整，无明显胃肠不适。",
            Objective = "合成检查数据稳定。",
            Assessment = "需要结合长期摄入继续评估。",
            Plan = "建议继续记录并复诊。"
        };

        return new RuntimeArchiveSample("older-adult-dietary-soap", archive);
    }

    public static RuntimeArchiveSample CreateDriConflict()
    {
        var archive = CreateArchive(7, "合成冲突样本", "男", 50, 175, 75, string.Empty);
        archive.DRIs = new DRIs(archive.Client)
        {
            AvailableDRIs =
            [
                Dri("锌", DietaryReferenceIntakeType.RNI, 10, "mg/d", false, "男", null),
                Dri("锌", DietaryReferenceIntakeType.RNI, 12, "mg/d", false, "男", null)
            ]
        };

        return new RuntimeArchiveSample("dri-unresolved-conflict", archive);
    }

    public static RuntimeArchiveSample CreateUnusualPhysiologyWithAdvice()
    {
        var archive = CreateArchive(8, "合成特殊组合", "男", 33, 172, 72, "孕晚期");
        var requestedAt = archive.ContractIdentity.CreatedAt.AddMinutes(20);
        archive.SubjectiveObjectiveAssessmentPlanInformation = new SubjectiveObjectiveAssessmentPlanInformation
        {
            Subjective = "合成主诉。",
            Objective = "合成客观资料。",
            Assessment = "由专业人员判断该组合。",
            Plan = "保留原始记录并复核。"
        };
        archive.AdvicePrompt = new PromptDto
        {
            PatientInfo = new PatientInfo
            {
                Gender = archive.Client.Gender,
                Age = archive.Client.Age,
                BMI = 24.34m,
                PAL = 1.5m,
                Height = archive.Client.Height,
                Weight = archive.Client.Weight,
                TotalBalanceEnergyViaCalculation = 2200,
                SpecialPhysiologicalPeriod = archive.Client.SpecialPhysiologicalPeriod
            },
            DietaryRecallSurvey = new EzNutrition.Shared.Data.DTO.PromptDto.DietaryRecallSurvey
            {
                DeficientNutrients = ["钙"],
                ExcessiveNutrients = ["钠"]
            },
            ClinicalInfo = new ClinicalInfo
            {
                Subjective = "合成主诉。",
                Objective = "合成客观资料。",
                Assessment = "合成评估。",
                Plan = "合成计划。"
            }
        };
        archive.AiGeneratedAdvice = new AiGeneratedAdvice
        {
            IsReady = true,
            GenerationStatus = AiAdviceGenerationStatus.Completed,
            RequestedAt = requestedAt,
            CompletedAt = requestedAt.AddSeconds(12),
            Environment = new EnvironmentDto("合成模型服务", "合成平台", "仅用于测试"),
            ReasoningContent = "合成推理摘要。",
            Content = "合成营养建议。"
        };

        return new RuntimeArchiveSample("unusual-physiology-ai", archive);
    }

    public static Task<RuntimeArchiveSample> CreateEmptyDietaryDraftAsync()
    {
        var archive = CreateArchive(9, "合成空白膳食草稿", "女", 26, 160, 52, string.Empty);
        var dris = new DRIs(archive.Client);
        var nutrients = CreateNutrients();
        var survey = new RuntimeDietaryRecallSurvey(archive.Client, CreateFoods(nutrients, 9), nutrients, dris);
        survey.Calculate();
        archive.DRIs = dris;
        archive.DietaryRecallSurvey = survey;
        return Task.FromResult(new RuntimeArchiveSample("empty-dietary-draft", archive));
    }

    internal static RuntimeArchive CreateArchive(
        int seed,
        string? name,
        string? gender,
        int age,
        decimal? height,
        decimal? weight,
        string physiologicalPeriod)
    {
        var client = new ClientInfo
        {
            ClientId = StableGuid(0x40000000, seed, 1),
            Name = name,
            Gender = gender,
            Age = age,
            Height = height,
            Weight = weight,
            SpecialPhysiologicalPeriod = physiologicalPeriod
        };
        var counter = 0;
        var identity = ArchiveContractIdentity.Create(
            BaseTime.AddDays(seed),
            () => StableGuid(0x50000000, seed, ++counter));
        return new RuntimeArchive(client, identity);
    }

    internal static EnergyCalculator CreateAutomaticEnergyCalculator(
        IClient client,
        decimal pal,
        decimal bee)
    {
        var calculator = new EnergyCalculator(client)
        {
            PAL = pal,
            AvailableEERs =
            [
                new EER
                {
                    Gender = client.Gender,
                    AgeStart = client.Age,
                    PAL = pal,
                    BEE = bee
                }
            ]
        };
        if (!calculator.Calculate())
        {
            throw new InvalidOperationException("合成样本的自动能量计算失败。");
        }

        return calculator;
    }

    internal static DietaryReferenceIntakeValue Dri(
        string nutrient,
        DietaryReferenceIntakeType type,
        decimal value,
        string unit,
        bool isOffset,
        string? gender,
        string? physiologicalPeriod) => new()
        {
            Nutrient = nutrient,
            RecordType = type,
            Value = value,
            MeasureUnit = unit,
            IsOffset = isOffset,
            Gender = gender,
            SpecialPhysiologicalPeriod = physiologicalPeriod,
            AgeStart = 18,
            Detail = "合成 DRIs 记录"
        };

    private static Task<RuntimeDietaryRecallSurvey> CreateDietaryRecallAsync(
        IClient client,
        DRIs dris,
        int seed)
    {
        var nutrients = CreateNutrients();
        var survey = new RuntimeDietaryRecallSurvey(client, CreateFoods(nutrients, seed), nutrients, dris);
        survey.RecallEntries.AddRange(
        [
            new DietaryRecallEntry
            {
                EntryId = StableGuid(0x60000000, seed, 1),
                Food = survey.Foods.ElementAt(0),
                Weight = 100,
                MealOccasion = MealOccasion.Breakfast,
                IsAllEdible = true
            },
            new DietaryRecallEntry
            {
                EntryId = StableGuid(0x60000000, seed, 2),
                Food = survey.Foods.ElementAt(1),
                Weight = 200,
                MealOccasion = MealOccasion.Lunch,
                IsAllEdible = false
            },
            new DietaryRecallEntry
            {
                EntryId = StableGuid(0x60000000, seed, 3),
                Food = survey.Foods.ElementAt(2),
                Weight = 200,
                MealOccasion = MealOccasion.Dinner,
                IsAllEdible = true
            }
        ]);
        survey.Calculate();
        return Task.FromResult(survey);
    }

    internal static List<Nutrient> CreateNutrients()
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

    internal static List<Food> CreateFoods(IReadOnlyList<Nutrient> nutrients, int seed)
    {
        return
        [
            Food(seed, 1, "SYN-A", "合成早餐", "谷类", 100, 165, 10, 5, 20, nutrients),
            Food(seed, 2, "SYN-B", "合成午餐", "动物性食品", 75, 190, 20, 10, 5, nutrients),
            Food(seed, 3, "SYN-C", "合成晚餐", "蔬菜类", 100, 120, 5, 0, 25, nutrients)
        ];
    }

    private static Food Food(
        int seed,
        int sequence,
        string code,
        string name,
        string group,
        int ediblePortion,
        decimal energy,
        decimal protein,
        decimal fat,
        decimal carbohydrate,
        IReadOnlyList<Nutrient> nutrients)
    {
        var food = new Food
        {
            FoodId = StableGuid(0x70000000, seed, sequence),
            FriendlyCode = code,
            FriendlyName = name,
            FoodGroups = group,
            EdiblePortion = ediblePortion
        };
        food.FoodNutrientValues =
        [
            FoodValue(food, nutrients, "能量", energy),
            FoodValue(food, nutrients, "蛋白质", protein),
            FoodValue(food, nutrients, "脂肪", fat),
            FoodValue(food, nutrients, "碳水化合物", carbohydrate)
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

    internal static Guid StableGuid(int prefix, int seed, int sequence) =>
        Guid.Parse($"{prefix:x8}-0000-0000-{seed:x4}-{sequence:x12}");
}
