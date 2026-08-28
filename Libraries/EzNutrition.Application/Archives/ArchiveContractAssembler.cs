using EzNutrition.Archives.Contracts.Abstractions;
using EzNutrition.Archives.Contracts.Bundles;
using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Archives.Contracts.Metadata;
using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Archives.Contracts.Serialization;
using EzNutrition.Archives.Contracts.ValueObjects;
using ContractChronologicalAge = EzNutrition.Archives.Contracts.ValueObjects.ChronologicalAge;
using EzNutrition.Application.Consultations;
using EzNutrition.Domain.Assessments;
using EzNutrition.Domain.Calculations;
using EzNutrition.Domain.Consultations;
using EzNutrition.Domain.Dietary;
using EzNutrition.Shared.Data.Entities;
using AiAdviceRequestDto = EzNutrition.Shared.Data.DTO.PromptDto.AiAdviceRequestDto;
using AdvicePatientAge = EzNutrition.Shared.Data.DTO.PromptDto.PatientAge;
using AdviceReferenceComparison = EzNutrition.Shared.Data.DTO.PromptDto.DietaryReferenceComparison;
using RuntimeWorkspace = EzNutrition.Application.Consultations.ConsultationWorkspace;

namespace EzNutrition.Application.Archives;

/// <summary>
/// 将 EzNutrition 运行态咨询工作区转换为格式无关档案契约。
/// </summary>
public sealed class ArchiveContractAssembler
{
    private readonly ApplicationIdentity sourceApplication;

    /// <summary>
    /// 初始化运行态档案转换器。
    /// </summary>
    /// <param name="sourceApplication">产生档案快照的应用身份。</param>
    public ArchiveContractAssembler(ApplicationIdentity sourceApplication)
    {
        ArgumentNullException.ThrowIfNull(sourceApplication);
        this.sourceApplication = sourceApplication;
    }

    /// <summary>
    /// 建立当前运行态咨询的类型化档案文档快照。
    /// </summary>
    /// <param name="archive">待转换的运行态咨询。</param>
    /// <param name="capturedAt">快照时间；未提供时使用当前 UTC 时间。</param>
    /// <param name="bundleId">可选的资源包标识。</param>
    /// <returns>包含当前已知资料的档案文档。</returns>
    public ArchiveDocument CreateDocument(
        RuntimeWorkspace archive,
        DateTimeOffset? capturedAt = null,
        ArchiveBundleId? bundleId = null)
    {
        ArgumentNullException.ThrowIfNull(archive);

        var captured = capturedAt ?? DateTimeOffset.UtcNow;
        if (captured < archive.ContractIdentity.CreatedAt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capturedAt),
                captured,
                "档案快照时间不能早于咨询会话建立时间。");
        }

        var identity = archive.ContractIdentity;
        var subjectReference = LogicalReference(identity.Patient, ArchiveResourceTypes.Patient);
        var consultationReference = ExactReference(identity.Consultation, ArchiveResourceTypes.Consultation);
        var clinicalResources = new List<IArchiveResource>();

        if (archive.CurrentEnergyCalculator is not null)
        {
            clinicalResources.Add(CreateEnergyAssessment(
                archive.CurrentEnergyCalculator,
                identity,
                subjectReference,
                consultationReference,
                captured));
        }

        if (archive.DRIs is not null)
        {
            clinicalResources.Add(CreateDriAssessment(
                archive.DRIs,
                identity,
                subjectReference,
                consultationReference,
                captured));
        }

        if (archive.DietaryRecallSurvey is not null)
        {
            clinicalResources.Add(CreateDietaryRecall(
                archive,
                identity,
                subjectReference,
                consultationReference,
                captured));
        }

        foreach (var assessment in archive.NutritionAssessments.Where(run => run.Answers.Count > 0))
        {
            clinicalResources.Add(CreateNutritionScaleAssessment(
                assessment,
                subjectReference,
                consultationReference,
                captured));
        }

        if (archive.SubjectiveObjectiveAssessmentPlanInformation is not null)
        {
            clinicalResources.Add(CreateSoapNote(
                archive.SubjectiveObjectiveAssessmentPlanInformation,
                identity,
                subjectReference,
                consultationReference,
                captured));
        }

        var adviceInputReferences = clinicalResources
            .Select(resource => new VersionedResourceReference(
                resource.Metadata.ResourceId,
                resource.Metadata.VersionId,
                resource.ResourceType))
            .ToArray();
        if (ShouldCreateNutritionAdvice(archive))
        {
            clinicalResources.Add(CreateNutritionAdvice(
                archive,
                identity,
                subjectReference,
                consultationReference,
                adviceInputReferences,
                captured));
        }

        var patient = CreatePatient(archive, captured);
        var consultation = CreateConsultation(
            archive,
            clinicalResources,
            subjectReference,
            captured);
        var entries = new List<IArchiveResource> { patient, consultation };
        entries.AddRange(clinicalResources);

        return new ArchiveDocument
        {
            Bundle = new ArchiveBundle
            {
                BundleId = bundleId ?? new ArchiveBundleId(Guid.NewGuid()),
                BundleType = ArchiveBundleType.ConsultationDocument,
                CreatedAt = captured,
                Producer = sourceApplication,
                Entries = entries
            }
        };
    }

    private PatientResource CreatePatient(RuntimeWorkspace archive, DateTimeOffset capturedAt)
    {
        if (archive.ExistingPatient is { } existingPatient)
        {
            return existingPatient;
        }

        var name = archive.Client.Name?.Trim();
        return new PatientResource
        {
            Metadata = Metadata(
                archive.ContractIdentity.Patient,
                capturedAt,
                archive.ContractIdentity.CreatedAt),
            IdentityMode = string.IsNullOrWhiteSpace(name)
                ? PatientIdentityMode.Unlinked
                : PatientIdentityMode.Identified,
            Names = string.IsNullOrWhiteSpace(name)
                ? []
                : [new HumanName { Text = name }],
            BirthDate = archive.Client.BirthDate is { } birthDate
                ? new PartialDate(birthDate.Year, birthDate.Month, birthDate.Day)
                : null,
            AdministrativeSex = string.IsNullOrWhiteSpace(archive.Client.Gender)
                ? null
                : ArchiveContractCoding.AdministrativeSex(archive.Client.Gender)
        };
    }

    private ConsultationResource CreateConsultation(
        RuntimeWorkspace archive,
        IReadOnlyList<IArchiveResource> clinicalResources,
        LogicalResourceReference subjectReference,
        DateTimeOffset capturedAt)
    {
        var physiologicalStates = string.IsNullOrWhiteSpace(archive.Client.SpecialPhysiologicalPeriod)
            ? Array.Empty<Coding>()
            : new[] { ArchiveContractCoding.PhysiologicalState(archive.Client.SpecialPhysiologicalPeriod) };
        var age = archive.Client.Age;
        var snapshot = new SubjectSnapshot
        {
            ChronologicalAgeAtConsultation = age is null
                ? null
                : new ContractChronologicalAge(age.Years, age.Months, age.Days),
            // 旧读取器仍可使用完整年数；精确年月日只由上面的结构化字段表达。
            AgeAtConsultation = age is null
                ? null
                : ArchiveContractCoding.Quantity(age.Years, "a"),
            AdministrativeSex = string.IsNullOrWhiteSpace(archive.Client.Gender)
                ? null
                : ArchiveContractCoding.AdministrativeSex(archive.Client.Gender),
            Height = archive.Client.Height is { } height
                ? Measurement(height, "cm", capturedAt)
                : null,
            Weight = archive.Client.Weight is { } weight
                ? Measurement(weight, "kg", capturedAt)
                : null,
            PhysiologicalStates = physiologicalStates,
            IdentityDisplay = NormalizeOptional(archive.Client.Name)
        };

        return new ConsultationResource
        {
            Metadata = Metadata(
                archive.ContractIdentity.Consultation,
                capturedAt,
                archive.ContractIdentity.CreatedAt),
            SubjectReference = subjectReference,
            Period = new Period(archive.ContractIdentity.CreatedAt),
            SubjectSnapshot = snapshot,
            ClinicalResourceReferences = clinicalResources.Select(resource =>
                new VersionedResourceReference(
                    resource.Metadata.ResourceId,
                    resource.Metadata.VersionId,
                    resource.ResourceType)).ToArray(),
            Title = string.IsNullOrWhiteSpace(archive.Client.Name)
                ? "营养咨询"
                : $"{archive.Client.Name.Trim()}的营养咨询"
        };
    }

    private EnergyAssessmentResource CreateEnergyAssessment(
        EnergyCalculator calculator,
        ArchiveContractIdentity identity,
        LogicalResourceReference subjectReference,
        VersionedResourceReference consultationReference,
        DateTimeOffset capturedAt)
    {
        const string candidateId = "automatic-energy";
        var candidates = new List<EnergyCalculationCandidate>();
        if (calculator.CalculatedEnergy is { } calculatedEnergy)
        {
            var inputs = CreateCommonAssessmentInputs(calculator.Client).ToList();
            if (calculator.PAL is { } pal)
            {
                inputs.Add(AssessmentInput(
                    "physical-activity-level",
                    "身体活动水平",
                    new DecimalArchiveValue(pal),
                    ClinicalValueSourceKind.Reported));
            }

            if (calculator.SelectedEer?.BEE is { } bee)
            {
                inputs.Add(AssessmentInput(
                    "basal-energy-coefficient",
                    "基础能量系数",
                    new DecimalArchiveValue(bee),
                    ClinicalValueSourceKind.Imported));
            }

            var intermediateResults = new List<NamedArchiveValue>();
            if (calculator.BMI is { } bmi)
            {
                intermediateResults.Add(NamedValue(
                    "body-mass-index",
                    "BMI",
                    new DecimalArchiveValue(bmi)));
            }

            if (calculator.AppliedOffsetEnergy != 0)
            {
                intermediateResults.Add(NamedValue(
                    "physiological-energy-offset",
                    "特殊生理状态能量偏移",
                    new QuantityArchiveValue(ArchiveContractCoding.Quantity(
                        calculator.AppliedOffsetEnergy,
                        "kcal/d"))));
            }

            var methodCode = calculator.CalculationMethod switch
            {
                EnergyCalculationMethod.IdealBodyWeightBeePal => "ideal-body-weight-bee-pal",
                EnergyCalculationMethod.PopulationAverage => "population-average-eer",
                _ => "legacy-energy-calculation"
            };
            candidates.Add(new EnergyCalculationCandidate
            {
                CandidateId = new LocalIdentifier(candidateId),
                Algorithm = Algorithm(methodCode, "WASM 自动能量计算"),
                Inputs = inputs,
                ReferenceData = [ArchiveContractCoding.EerReferenceData()],
                Result = ArchiveContractCoding.Quantity(calculatedEnergy, "kcal/d"),
                IntermediateResults = intermediateResults
            });
        }

        ProfessionalEnergyDecision? decision = null;
        if (calculator.Energy is { } adoptedEnergy)
        {
            decision = new ProfessionalEnergyDecision
            {
                AdoptedEnergyTarget = ArchiveContractCoding.Quantity(adoptedEnergy, "kcal/d"),
                SelectedCandidateId = calculator.CalculatedEnergy is null
                    ? null
                    : new LocalIdentifier(candidateId),
                DecisionBasis = calculator.IsEnergyManuallyAdjusted
                    ? ArchiveContractCoding.Code(
                        "energy-decision-basis",
                        "professional-adjustment",
                        "专业人员手工核定")
                    : ArchiveContractCoding.Code(
                        "energy-decision-basis",
                        "automatic-calculation",
                        "采用自动计算结果"),
                Reason = calculator.IsEnergyManuallyAdjusted
                    ? "专业人员在能量核定界面手工调整。"
                    : null
            };
        }

        return new EnergyAssessmentResource
        {
            Metadata = Metadata(identity.EnergyAssessment, capturedAt, identity.CreatedAt),
            SubjectReference = subjectReference,
            ConsultationReference = consultationReference,
            EffectiveAt = capturedAt,
            CandidateCalculations = candidates,
            ProfessionalDecision = decision,
            ProfessionalDecisionAbsentReason = decision is null
                ? DataAbsentReasonCode.NotEstablished
                : null,
            AllocationPlan = CreateAllocationPlan(calculator)
        };
    }

    private EnergyAllocationPlan? CreateAllocationPlan(EnergyCalculator calculator)
    {
        if (calculator.Allocation is not { } allocation || calculator.Energy is not { } energy)
        {
            return null;
        }

        var macronutrients = new[]
        {
            MacronutrientTarget(
                "蛋白质",
                allocation.ProteinPercentage,
                allocation.TotalProteinContent,
                allocation.BreakfastProteinContent,
                allocation.LunchProteinContent,
                allocation.DinnerProteinContent),
            MacronutrientTarget(
                "碳水化合物",
                allocation.CarbohydratePercentage,
                allocation.TotalCarbohydrateContent,
                allocation.BreakfastCarbohydrateContent,
                allocation.LunchCarbohydrateContent,
                allocation.DinnerCarbohydrateContent),
            MacronutrientTarget(
                "总脂肪",
                allocation.FatPercentage,
                allocation.TotalFatContent,
                allocation.BreakfastFatContent,
                allocation.LunchFatContent,
                allocation.DinnerFatContent)
        };

        var exchanges = new List<FoodExchangeTarget>();
        if (calculator.FoodExchangeAllocation is { } foodExchange)
        {
            exchanges.Add(FoodExchange("grains-and-starchy-foods", "谷薯类", foodExchange.GrainsAndStarchyFoods));
            exchanges.Add(FoodExchange("fruits", "水果类", foodExchange.Fruits));
            exchanges.Add(FoodExchange("vegetables", "蔬菜类", foodExchange.Vegetables));
            exchanges.Add(FoodExchange("meats-and-eggs", "肉蛋类", foodExchange.MeatsAndEggs));
            exchanges.Add(FoodExchange(
                "legumes-and-dairy-alternatives",
                "豆乳类",
                foodExchange.LegumesAndDairyAlternatives));
            exchanges.Add(FoodExchange("energy-foods-or-fats", "油脂与高能食物", foodExchange.EnergyFoodsOrFats));
        }

        return new EnergyAllocationPlan
        {
            Method = Algorithm("macronutrient-and-food-exchange-allocation", "宏量营养素与食物交换分配"),
            EnergyTarget = ArchiveContractCoding.Quantity(energy, "kcal/d"),
            MacronutrientTargets = macronutrients,
            FoodExchangeTargets = exchanges
        };
    }

    private DriAssessmentResource CreateDriAssessment(
        DRIs dris,
        ArchiveContractIdentity identity,
        LogicalResourceReference subjectReference,
        VersionedResourceReference consultationReference,
        DateTimeOffset capturedAt)
    {
        var results = dris.AvailableDRIs
            .Where(record => !string.IsNullOrWhiteSpace(record.Nutrient))
            .GroupBy(record => record.Nutrient!.Trim(), StringComparer.Ordinal)
            .Select(group => new NutrientReferenceResult
            {
                Nutrient = ArchiveContractCoding.Nutrient(group.Key),
                ReferenceValues = group
                    .GroupBy(record => record.RecordType)
                    .Select(CreateDriReferenceValue)
                    .ToArray()
            })
            .ToArray();
        var populationDisplay = string.Join(
            " / ",
            new[]
            {
                NormalizeOptional(dris.Client.Gender) ?? "性别未说明",
                dris.Client.Age?.ToString() ?? "年龄未说明",
                NormalizeOptional(dris.Client.SpecialPhysiologicalPeriod)
            }.Where(value => value is not null));
        var population = ArchiveContractCoding.Code(
            "dri-population-selection",
            ArchiveContractCoding.StableCode("population", populationDisplay),
            populationDisplay);

        return new DriAssessmentResource
        {
            Metadata = Metadata(identity.DriAssessment, capturedAt, identity.CreatedAt),
            SubjectReference = subjectReference,
            ConsultationReference = consultationReference,
            EffectiveAt = capturedAt,
            InputContext = CreateCommonAssessmentInputs(dris.Client).ToArray(),
            Selector = Algorithm("server-dri-population-selector", "服务端 DRIs 人群筛选"),
            ReferenceData = ArchiveContractCoding.DriReferenceData(),
            PopulationGroup = new PopulationGroupSelection
            {
                BasisGroup = population,
                AdoptedGroup = population
            },
            NutrientResults = results
        };
    }

    private DriReferenceValue CreateDriReferenceValue(
        IGrouping<DietaryReferenceIntakeType, DietaryReferenceIntakeValue> group)
    {
        var records = group.ToArray();
        var components = records.Select(record => new DriReferenceComponent
        {
            Value = ArchiveContractCoding.Quantity(record.Value, record.MeasureUnit),
            IsOffset = record.IsOffset,
            MinimumAge = record.AgeStart is { } age
                ? ArchiveContractCoding.Quantity(age, "a")
                : null,
            PopulationSex = string.IsNullOrWhiteSpace(record.Gender)
                ? null
                : ArchiveContractCoding.AdministrativeSex(record.Gender),
            PhysiologicalState = string.IsNullOrWhiteSpace(record.SpecialPhysiologicalPeriod)
                ? null
                : ArchiveContractCoding.PhysiologicalState(record.SpecialPhysiologicalPeriod),
            Detail = NormalizeOptional(record.Detail)
        }).ToArray();
        var adoptedValue = ResolveDriValue(records);
        var archiveValue = adoptedValue is null
            ? null
            : new QuantityArchiveValue(adoptedValue);
        var code = group.Key.ToString();

        return new DriReferenceValue
        {
            ReferenceType = ArchiveContractCoding.Code(
                "dri-reference-type",
                code,
                code),
            BasisValue = archiveValue,
            AdoptedValue = archiveValue,
            AbsentReason = archiveValue is null ? DataAbsentReasonCode.NotEstablished : null,
            Components = components
        };
    }

    private static Quantity? ResolveDriValue(IReadOnlyList<DietaryReferenceIntakeValue> records)
    {
        var selectedRecords = records.ToList();
        var absoluteRecords = selectedRecords.Where(record => !record.IsOffset).ToArray();
        if (absoluteRecords.Length == 2)
        {
            var specificAbsoluteRecords = absoluteRecords
                .Where(record => !string.IsNullOrWhiteSpace(record.SpecialPhysiologicalPeriod))
                .ToArray();
            if (specificAbsoluteRecords.Length == 1)
            {
                var selectedAbsolute = specificAbsoluteRecords[0];
                selectedRecords = selectedRecords
                    .Where(record => record.IsOffset || ReferenceEquals(record, selectedAbsolute))
                    .ToList();
            }
        }

        if (selectedRecords.Count(record => !record.IsOffset) != 1)
        {
            return null;
        }

        var units = selectedRecords
            .Select(record => ArchiveContractCoding.Unit(record.MeasureUnit))
            .Distinct()
            .ToArray();
        return units.Length == 1
            ? new Quantity(selectedRecords.Sum(record => record.Value), units[0])
            : null;
    }

    private DietaryRecallResource CreateDietaryRecall(
        RuntimeWorkspace archive,
        ArchiveContractIdentity identity,
        LogicalResourceReference subjectReference,
        VersionedResourceReference consultationReference,
        DateTimeOffset capturedAt)
    {
        var survey = archive.DietaryRecallSurvey!;
        var entries = survey.RecallEntries;
        var meals = entries
            .GroupBy(entry => entry.MealOccasion)
            .OrderBy(group => (int)group.Key)
            .Select((group, index) => CreateMealRecall(survey, group.Key, group.ToArray(), index + 1))
            .ToArray();
        var totalSummary = survey.SummaryCalculationTable is null
            ? Array.Empty<NutrientAmount>()
            : survey.Nutrients
                .Select(nutrient => new NutrientAmount
                {
                    Nutrient = ArchiveContractCoding.Nutrient(nutrient.FriendlyName),
                    Amount = ArchiveContractCoding.Quantity(
                        survey.SummaryCalculationTable[nutrient],
                        nutrient.DefaultMeasureUnit)
                })
                .ToArray();

        return new DietaryRecallResource
        {
            Metadata = Metadata(identity.DietaryRecall, capturedAt, identity.CreatedAt),
            SubjectReference = subjectReference,
            ConsultationReference = consultationReference,
            RecallPeriod = null,
            RecallPeriodAbsentReason = DataAbsentReasonCode.NotAsked,
            RecallMethod = ArchiveContractCoding.Code(
                "dietary-recall-method",
                "24-hour-recall",
                "24 小时膳食回顾法"),
            Status = entries.Count == 0 ? null : DietaryRecallStatus.IntakeReported,
            Meals = meals,
            TotalNutrientSummary = totalSummary,
            EnergyConsistency = CreateEnergyConsistency(survey),
            GuidanceSnapshot = CreateGuidanceSnapshot(archive.DietaryTower)
        };
    }

    private MealRecall CreateMealRecall(
        DietaryRecallSurvey survey,
        MealOccasion occasion,
        IReadOnlyList<DietaryRecallEntry> entries,
        int sequence)
    {
        var mappedEntries = entries.Select((entry, index) => CreateFoodEntry(survey, entry, index + 1)).ToArray();
        var summary = survey.SummaryCalculationTable is null
            ? Array.Empty<NutrientAmount>()
            : survey.SummaryCalculationTable[occasion]
                .Where(value => value.Nutrient is not null)
                .Select(NutrientAmount)
                .ToArray();

        return new MealRecall
        {
            MealId = new LocalIdentifier($"meal-{(int)occasion}"),
            Occasion = ArchiveContractCoding.MealOccasion(occasion),
            Sequence = sequence,
            Entries = mappedEntries,
            NutrientSummary = summary
        };
    }

    private FoodIntakeEntry CreateFoodEntry(
        DietaryRecallSurvey survey,
        DietaryRecallEntry entry,
        int sequence)
    {
        var edibleFraction = entry.IsAllEdible
            ? 1m
            : (entry.Food.EdiblePortion ?? 100) / 100m;
        var consumedAmount = entry.Weight * edibleFraction;
        var contributions = (entry.Food.FoodNutrientValues ?? [])
            .Select(value =>
            {
                var nutrient = value.Nutrient ?? survey.Nutrients.FirstOrDefault(candidate =>
                    candidate.NutrientId == value.NutrientId);
                return nutrient is null
                    ? null
                    : new NutrientAmount
                    {
                        Nutrient = ArchiveContractCoding.Nutrient(nutrient.FriendlyName),
                        Amount = ArchiveContractCoding.Quantity(
                            value.Value * consumedAmount / 100m,
                            value.MeasureUnit ?? nutrient.DefaultMeasureUnit)
                    };
            })
            .Where(value => value is not null)
            .Cast<NutrientAmount>()
            .ToArray();

        return new FoodIntakeEntry
        {
            EntryId = new LocalIdentifier($"entry-{entry.EntryId:D}"),
            Food = ArchiveContractCoding.Food(entry.Food),
            ReportedAmount = ArchiveContractCoding.Quantity(entry.Weight, "g"),
            EdibleFraction = edibleFraction,
            AdoptedConsumedAmount = ArchiveContractCoding.Quantity(consumedAmount, "g"),
            FoodCompositionData = ArchiveContractCoding.FoodCompositionReferenceData(),
            NutrientContributions = contributions,
            Sequence = sequence
        };
    }

    private static NutrientAmount NutrientAmount(FoodNutrientValue value) => new()
    {
        Nutrient = ArchiveContractCoding.Nutrient(value.Nutrient?.FriendlyName),
        Amount = ArchiveContractCoding.Quantity(
            value.Value,
            value.MeasureUnit ?? value.Nutrient?.DefaultMeasureUnit)
    };

    private DietaryEnergyConsistency? CreateEnergyConsistency(DietaryRecallSurvey survey)
    {
        if (survey.SummaryCalculationTable is not { } table)
        {
            return null;
        }

        var nutrients = survey.Nutrients.ToArray();
        var energy = nutrients.FirstOrDefault(nutrient => nutrient.FriendlyName == "能量");
        var protein = nutrients.FirstOrDefault(nutrient => nutrient.FriendlyName == "蛋白质");
        var fat = nutrients.FirstOrDefault(nutrient => nutrient.FriendlyName == "脂肪");
        var carbohydrate = nutrients.FirstOrDefault(nutrient => nutrient.FriendlyName == "碳水化合物");
        if (energy is null || protein is null || fat is null || carbohydrate is null)
        {
            return null;
        }

        var derivedEnergy = (table[protein] * 4m) + (table[fat] * 9m) + (table[carbohydrate] * 4m);
        return new DietaryEnergyConsistency
        {
            Method = Algorithm("atwater-general-factors", "宏量营养素通用折算系数"),
            RecordedTotalEnergy = ArchiveContractCoding.Quantity(table[energy], energy.DefaultMeasureUnit),
            MacronutrientDerivedEnergy = ArchiveContractCoding.Quantity(derivedEnergy, energy.DefaultMeasureUnit),
            AllowedDifference = null,
            AllowedDifferenceAbsentReason = DataAbsentReasonCode.NotEstablished
        };
    }

    private DietaryGuidanceSnapshot? CreateGuidanceSnapshot(DietaryTower? tower)
    {
        if (tower is null)
        {
            return null;
        }

        return new DietaryGuidanceSnapshot
        {
            Method = Algorithm("dietary-guideline-pagoda-comparison", "膳食宝塔比较"),
            Guideline = ArchiveContractCoding.DietaryGuidelineReferenceData(),
            Items = tower.RenderTower().Select(CreateGuidanceItem).ToArray()
        };
    }

    private static DietaryGuidanceItem CreateGuidanceItem(TowerLayer layer) => new()
    {
        Category = ArchiveContractCoding.FoodGroup(layer.LayerName),
        ObservedValue = string.IsNullOrWhiteSpace(layer.DietaryRecallTower)
            ? null
            : new TextArchiveValue(layer.DietaryRecallTower),
        Recommendation = NormalizeOptional(layer.StandardTowerValue),
        Children = layer.Children?.Select(CreateGuidanceItem).ToArray() ?? []
    };

    private SoapNoteResource CreateSoapNote(
        SubjectiveObjectiveAssessmentPlanInformation information,
        ArchiveContractIdentity identity,
        LogicalResourceReference subjectReference,
        VersionedResourceReference consultationReference,
        DateTimeOffset capturedAt) => new()
        {
            Metadata = Metadata(identity.SoapNote, capturedAt, identity.CreatedAt),
            SubjectReference = subjectReference,
            ConsultationReference = consultationReference,
            EffectiveAt = capturedAt,
            Subjective = NormalizeOptional(information.Subjective),
            Objective = NormalizeOptional(information.Objective),
            Assessment = NormalizeOptional(information.Assessment),
            Plan = NormalizeOptional(information.Plan)
        };

    private NutritionScaleAssessmentResource CreateNutritionScaleAssessment(
        NutritionAssessmentRun run,
        LogicalResourceReference subjectReference,
        VersionedResourceReference consultationReference,
        DateTimeOffset capturedAt)
    {
        var definition = run.Definition;
        var evaluation = run.Evaluation;
        var responses = definition.Items
            .Where(item => evaluation.ApplicableItemCodes.Contains(item.Code))
            .Select(item =>
            {
                if (!run.Answers.TryGetValue(item.Code, out var answerCode))
                {
                    return null;
                }

                var option = item.Options.Single(option =>
                    string.Equals(option.Code, answerCode, StringComparison.Ordinal));
                return new AssessmentItemResponse
                {
                    Item = AssessmentCoding(
                        definition,
                        $"{definition.Code}/item/{item.Code}",
                        item.Prompt),
                    Answer = new CodingArchiveValue(AssessmentCoding(
                        definition,
                        $"{definition.Code}/item/{item.Code}/answer/{option.Code}",
                        option.Display)),
                    ScoreContribution = option.Score
                };
            })
            .Where(response => response is not null)
            .Cast<AssessmentItemResponse>()
            .ToArray();

        return new NutritionScaleAssessmentResource
        {
            Metadata = Metadata(run.ArchiveIdentity, capturedAt, run.CreatedAt),
            SubjectReference = subjectReference,
            ConsultationReference = consultationReference,
            EffectiveAt = run.CompletedAt ?? run.LastModifiedAt,
            Instrument = new AssessmentInstrumentIdentity
            {
                Code = new Coding(
                    definition.CodeSystem,
                    definition.Code,
                    definition.Version,
                    definition.DisplayName),
                Version = definition.Version,
                Definition = new CanonicalReference(definition.DefinitionUri, definition.Version)
            },
            Responses = responses,
            DerivedResults = evaluation.Metrics.Select(metric => new NamedArchiveValue
            {
                Name = AssessmentCoding(
                    definition,
                    $"{definition.Code}/result/{metric.Code}",
                    metric.Display),
                Value = new DecimalArchiveValue(metric.Value)
            }).ToArray(),
            ScoringMethod = new AlgorithmIdentity
            {
                Method = AssessmentCoding(
                    definition,
                    $"{definition.Code}/scoring",
                    $"{definition.DisplayName}确定性计分"),
                Implementation = sourceApplication
            },
            TotalScore = evaluation.TotalScore,
            TotalScoreAbsentReason = evaluation.TotalScore is null
                ? evaluation.IsComplete
                    ? DataAbsentReasonCode.NotApplicable
                    : DataAbsentReasonCode.NotEstablished
                : null,
            Interpretation = evaluation.Interpretation is { } interpretation
                ? AssessmentCoding(
                    definition,
                    $"{definition.Code}/interpretation/{interpretation.Code}",
                    interpretation.Display)
                : null
        };
    }

    private NutritionAdviceResource CreateNutritionAdvice(
        RuntimeWorkspace archive,
        ArchiveContractIdentity identity,
        LogicalResourceReference subjectReference,
        VersionedResourceReference consultationReference,
        IReadOnlyList<VersionedResourceReference> inputResourceReferences,
        DateTimeOffset capturedAt)
    {
        var advice = archive.AiGeneratedAdvice;
        var environment = advice?.Environment;
        AlgorithmIdentity? generator = null;
        if (!string.IsNullOrWhiteSpace(environment?.ProviderName))
        {
            generator = new AlgorithmIdentity
            {
                Method = new Coding(
                    ArchiveContractCoding.CodeSystem("ai-provider"),
                    ArchiveContractCoding.StableCode("provider", environment.ProviderName),
                    display: environment.ProviderName)
            };
        }

        var createdAt = advice?.RequestedAt ?? capturedAt;
        return new NutritionAdviceResource
        {
            Metadata = Metadata(identity.NutritionAdvice, capturedAt, createdAt),
            SubjectReference = subjectReference,
            ConsultationReference = consultationReference,
            GenerationStatus = MapAdviceStatus(advice),
            RequestedAt = advice?.RequestedAt,
            CompletedAt = advice?.CompletedAt,
            Generator = generator,
            GeneratorDetails = environment is null
                ? null
                : string.Join(
                    "；",
                    new[] { environment.PlatformDetails, environment.AdditionalInfo }
                        .Where(value => !string.IsNullOrWhiteSpace(value))),
            InputResourceReferences = inputResourceReferences,
            InputSummary = CreateAdviceInputSummary(archive.AdvicePrompt),
            ReasoningContent = NormalizeOptional(advice?.ReasoningContent),
            NarrativeContent = NormalizeOptional(advice?.Content)
        };
    }

    private static bool ShouldCreateNutritionAdvice(RuntimeWorkspace archive) =>
        archive.AdvicePrompt is not null ||
        archive.AiGeneratedAdvice is
        {
            Sending: true
        } ||
        !string.IsNullOrWhiteSpace(archive.AiGeneratedAdvice?.ReasoningContent) ||
        !string.IsNullOrWhiteSpace(archive.AiGeneratedAdvice?.Content);

    private static NutritionAdviceGenerationStatus MapAdviceStatus(AiGeneratedAdvice? advice) =>
        advice?.GenerationStatus switch
        {
            AiAdviceGenerationStatus.Generating => NutritionAdviceGenerationStatus.Generating,
            AiAdviceGenerationStatus.Completed => NutritionAdviceGenerationStatus.Completed,
            AiAdviceGenerationStatus.Incomplete => NutritionAdviceGenerationStatus.Incomplete,
            AiAdviceGenerationStatus.Failed => NutritionAdviceGenerationStatus.Failed,
            _ => NutritionAdviceGenerationStatus.Prepared
        };

    private static IReadOnlyList<NamedArchiveValue> CreateAdviceInputSummary(AiAdviceRequestDto? prompt)
    {
        if (prompt is null)
        {
            return [];
        }

        var inputs = new List<NamedArchiveValue>();
        Add(inputs, "age", "年龄", new TextArchiveValue(FormatAdviceAge(prompt.PatientInfo.Age)));
        if (!string.IsNullOrWhiteSpace(prompt.PatientInfo.Gender))
        {
            Add(inputs, "administrative-sex", "性别", new CodingArchiveValue(
                ArchiveContractCoding.AdministrativeSex(prompt.PatientInfo.Gender)));
        }

        if (prompt.PatientInfo.BMI is { } bmi)
        {
            Add(inputs, "body-mass-index", "BMI", new DecimalArchiveValue(bmi));
        }

        if (prompt.PatientInfo.PAL is { } pal)
        {
            Add(inputs, "physical-activity-level", "身体活动水平", new DecimalArchiveValue(pal));
        }

        if (prompt.PatientInfo.Height is { } height)
        {
            Add(inputs, "height", "身高", new QuantityArchiveValue(ArchiveContractCoding.Quantity(height, "cm")));
        }

        if (prompt.PatientInfo.Weight is { } weight)
        {
            Add(inputs, "weight", "体重", new QuantityArchiveValue(ArchiveContractCoding.Quantity(weight, "kg")));
        }

        if (prompt.PatientInfo.TotalBalanceEnergyViaCalculation is { } energy)
        {
            Add(inputs, "adopted-energy", "核定能量", new QuantityArchiveValue(
                ArchiveContractCoding.Quantity(energy, "kcal/d")));
        }

        if (!string.IsNullOrWhiteSpace(prompt.PatientInfo.SpecialPhysiologicalPeriod))
        {
            Add(inputs, "physiological-state", "特殊生理状态", new CodingArchiveValue(
                ArchiveContractCoding.PhysiologicalState(prompt.PatientInfo.SpecialPhysiologicalPeriod)));
        }

        foreach (var nutrient in prompt.DietaryRecallSurvey?.Nutrients
            .Where(nutrient =>
                nutrient.ReferenceComparison == AdviceReferenceComparison.BelowReference)
            .Select(nutrient => nutrient.Name) ?? [])
        {
            Add(inputs, "deficient-nutrient", "摄入不足营养素", new CodingArchiveValue(
                ArchiveContractCoding.Nutrient(nutrient)));
        }

        foreach (var nutrient in prompt.DietaryRecallSurvey?.Nutrients
            .Where(nutrient =>
                nutrient.ReferenceComparison == AdviceReferenceComparison.AboveReference)
            .Select(nutrient => nutrient.Name) ?? [])
        {
            Add(inputs, "excessive-nutrient", "摄入过量营养素", new CodingArchiveValue(
                ArchiveContractCoding.Nutrient(nutrient)));
        }

        return inputs;
    }

    private static string FormatAdviceAge(AdvicePatientAge age)
    {
        var value = $"{age.Years}岁";
        if (age.Months is { } months)
        {
            value += $"{months}个月";
        }

        if (age.Days is { } days)
        {
            value += $"{days}天";
        }

        return value;
    }

    private static void Add(
        ICollection<NamedArchiveValue> target,
        string code,
        string display,
        ArchiveValue value) => target.Add(NamedValue(code, display, value));

    private IEnumerable<AssessmentInput> CreateCommonAssessmentInputs(IClient client)
    {
        if (client.Age is { } age)
        {
            yield return AssessmentInput(
                "age",
                "年龄",
                new QuantityArchiveValue(ArchiveContractCoding.Quantity(age.ToReferenceYears(), "a")),
                client.BirthDate is null
                    ? ClinicalValueSourceKind.Reported
                    : ClinicalValueSourceKind.Derived);
        }

        if (!string.IsNullOrWhiteSpace(client.Gender))
        {
            yield return AssessmentInput(
                "calculation-sex",
                "计算采用性别",
                new CodingArchiveValue(ArchiveContractCoding.AdministrativeSex(client.Gender)),
                ClinicalValueSourceKind.Reported);
        }

        if (client.Height is { } height)
        {
            yield return AssessmentInput(
                "height",
                "身高",
                new QuantityArchiveValue(ArchiveContractCoding.Quantity(height, "cm")),
                ClinicalValueSourceKind.Reported);
        }

        if (client.Weight is { } weight)
        {
            yield return AssessmentInput(
                "weight",
                "体重",
                new QuantityArchiveValue(ArchiveContractCoding.Quantity(weight, "kg")),
                ClinicalValueSourceKind.Reported);
        }

        if (!string.IsNullOrWhiteSpace(client.SpecialPhysiologicalPeriod))
        {
            yield return AssessmentInput(
                "physiological-state",
                "特殊生理状态",
                new CodingArchiveValue(ArchiveContractCoding.PhysiologicalState(client.SpecialPhysiologicalPeriod)),
                ClinicalValueSourceKind.Reported);
        }
    }

    private static AssessmentInput AssessmentInput(
        string code,
        string display,
        ArchiveValue value,
        ClinicalValueSourceKind sourceKind) => new()
        {
            Parameter = ArchiveContractCoding.Code("assessment-parameter", code, display),
            AdoptedValue = value,
            SourceKind = sourceKind
        };

    private AlgorithmIdentity Algorithm(string code, string display) => new()
    {
        Method = ArchiveContractCoding.Code("algorithm", code, display, sourceApplication.Version),
        Implementation = sourceApplication
    };

    private static NamedArchiveValue NamedValue(string code, string display, ArchiveValue value) => new()
    {
        Name = ArchiveContractCoding.Code("named-value", code, display),
        Value = value
    };

    private static Coding AssessmentCoding(
        NutritionAssessmentDefinition definition,
        string code,
        string display) => new(
            definition.CodeSystem,
            code,
            definition.Version,
            display);

    private ResourceMetadata Metadata(
        ArchiveResourceIdentity identity,
        DateTimeOffset capturedAt,
        DateTimeOffset createdAt)
    {
        return new ResourceMetadata
        {
            ResourceId = identity.ResourceId,
            VersionId = identity.VersionId,
            RevisionNumber = new RevisionNumber(1),
            Status = ResourceLifecycleStatus.Draft,
            CreatedAt = createdAt,
            LastModifiedAt = capturedAt < createdAt ? createdAt : capturedAt,
            SourceApplication = sourceApplication
        };
    }

    private static LogicalResourceReference LogicalReference(
        ArchiveResourceIdentity identity,
        ResourceTypeCode type) => new(identity.ResourceId, type);

    private static VersionedResourceReference ExactReference(
        ArchiveResourceIdentity identity,
        ResourceTypeCode type) => new(identity.ResourceId, identity.VersionId, type);

    private static ClinicalMeasurement Measurement(
        decimal value,
        string unit,
        DateTimeOffset effectiveAt) => new()
        {
            Value = ArchiveContractCoding.Quantity(value, unit),
            EffectiveAt = effectiveAt,
            SourceKind = ClinicalValueSourceKind.Reported
        };

    private static MacronutrientAllocationTarget MacronutrientTarget(
        string display,
        double energyFraction,
        double dailyAmount,
        double breakfastAmount,
        double lunchAmount,
        double dinnerAmount) => new()
        {
            Nutrient = ArchiveContractCoding.Nutrient(display),
            EnergyFraction = Convert.ToDecimal(energyFraction),
            DailyAmount = ArchiveContractCoding.Quantity(Convert.ToDecimal(dailyAmount), "g/d"),
            MealAllocations =
            [
                MealAmount(MealOccasion.Breakfast, breakfastAmount),
                MealAmount(MealOccasion.Lunch, lunchAmount),
                MealAmount(MealOccasion.Dinner, dinnerAmount)
            ]
        };

    private static MealNutrientAllocation MealAmount(MealOccasion occasion, double amount) => new()
    {
        MealOccasion = ArchiveContractCoding.MealOccasion(occasion),
        Amount = ArchiveContractCoding.Quantity(Convert.ToDecimal(amount), "g")
    };

    private static FoodExchangeTarget FoodExchange(string code, string display, double value) => new()
    {
        FoodGroup = ArchiveContractCoding.Code("food-exchange-group", code, display),
        DailyExchanges = ArchiveContractCoding.Quantity(Convert.ToDecimal(value), "exchange/d")
    };

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
