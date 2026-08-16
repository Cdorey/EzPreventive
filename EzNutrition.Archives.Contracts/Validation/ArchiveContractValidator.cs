using EzNutrition.Archives.Contracts.Abstractions;
using EzNutrition.Archives.Contracts.Bundles;
using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Archives.Contracts.Metadata;
using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Archives.Contracts.ValueObjects;

namespace EzNutrition.Archives.Contracts.Validation;

/// <summary>
/// 对格式无关档案执行安全、生命周期、引用闭包和可确定复算的内部完整性校验。
/// </summary>
/// <remarks>
/// 校验器不访问网络、数据库、文件系统或具体交换格式，也不判断临床事实是否合理。
/// </remarks>
public sealed class ArchiveContractValidator : IArchiveValidator
{
    private const int MaximumExtensionDepth = 32;

    /// <inheritdoc />
    public ArchiveValidationResult ValidateResource(
        IArchiveResource resource,
        ArchiveValidationScope scope)
    {
        ArgumentNullException.ThrowIfNull(resource);
        var issues = new List<ArchiveValidationIssue>();
        ValidateResourceCore(resource, scope, string.Empty, issues);
        return new ArchiveValidationResult { Issues = issues };
    }

    /// <inheritdoc />
    public ArchiveValidationResult ValidateBundle(
        ArchiveBundle bundle,
        ArchiveValidationScope scope)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        var issues = new List<ArchiveValidationIssue>();
        ValidateExtensions(bundle.Extensions, "/Extensions", null, issues);

        var entries = bundle.Entries;
        for (var index = 0; index < entries.Count; index++)
        {
            ValidateResourceCore(entries[index], scope, $"/Entries/{index}", issues);
        }

        ValidateUniqueResourceVersions(entries, issues);
        ValidateBundleReferences(bundle, issues);
        ValidateRevisionGraph(entries, issues);
        ValidateConsultationDocument(bundle, issues);
        return new ArchiveValidationResult { Issues = issues };
    }

    private static void ValidateResourceCore(
        IArchiveResource resource,
        ArchiveValidationScope scope,
        string prefix,
        ICollection<ArchiveValidationIssue> issues)
    {
        ValidateMetadata(resource, scope, prefix, issues);
        switch (resource)
        {
            case PatientResource patient:
                ValidatePatient(patient, scope, prefix, issues);
                break;
            case ConsultationResource consultation:
                ValidateConsultation(consultation, prefix, issues);
                break;
            case EnergyAssessmentResource energy:
                ValidateEnergyAssessment(energy, scope, prefix, issues);
                break;
            case DriAssessmentResource dri:
                ValidateDriAssessment(dri, scope, prefix, issues);
                break;
            case DietaryRecallResource recall:
                ValidateDietaryRecall(recall, scope, prefix, issues);
                break;
            case SoapNoteResource soap:
                ValidateAssessmentReferenceTypes(soap.SubjectReference, soap.ConsultationReference, prefix, soap, issues);
                break;
            case NutritionAdviceResource advice:
                ValidateNutritionAdvice(advice, scope, prefix, issues);
                break;
            case NutritionReportResource report:
                ValidateNutritionReport(report, scope, prefix, issues);
                break;
            case NutritionScaleAssessmentResource scale:
                ValidateNutritionScaleAssessment(scale, scope, prefix, issues);
                break;
        }
    }

    private static void ValidateMetadata(
        IArchiveResource resource,
        ArchiveValidationScope scope,
        string prefix,
        ICollection<ArchiveValidationIssue> issues)
    {
        var metadata = resource.Metadata;
        if (metadata.LastModifiedAt < metadata.CreatedAt ||
            metadata.FinalizedAt < metadata.CreatedAt ||
            metadata.EnteredInErrorAt < metadata.CreatedAt)
        {
            AddIssue(
                issues,
                ArchiveValidationCodes.InvalidResourceTimeline,
                ArchiveValidationSeverity.Error,
                ArchiveValidationCategory.Integrity,
                "资源时间戳的先后顺序无效。",
                Path(prefix, "/Metadata"),
                resource);
        }

        if (metadata.BasedOn is not null && metadata.Supersedes is not null)
        {
            AddIssue(
                issues,
                ArchiveValidationCodes.InvalidRevisionRelationship,
                ArchiveValidationSeverity.Error,
                ArchiveValidationCategory.Integrity,
                "同一版本不能同时声明编辑来源和正式替代关系。",
                Path(prefix, "/Metadata"),
                resource);
        }

        if (IsSelfReference(metadata.BasedOn, metadata) || IsSelfReference(metadata.Supersedes, metadata))
        {
            AddIssue(
                issues,
                ArchiveValidationCodes.InvalidRevisionRelationship,
                ArchiveValidationSeverity.Error,
                ArchiveValidationCategory.Integrity,
                "资源版本不能引用自身作为修订来源。",
                Path(prefix, "/Metadata"),
                resource);
        }

        switch (metadata.Status)
        {
            case ResourceLifecycleStatus.Draft:
                if (metadata.FinalizedAt is not null || metadata.FinalizedBy is not null || metadata.Supersedes is not null)
                {
                    AddIssue(
                        issues,
                        ArchiveValidationCodes.InvalidLifecycleState,
                        ArchiveValidationSeverity.Error,
                        ArchiveValidationCategory.Integrity,
                        "草稿包含仅适用于正式版本的生命周期字段。",
                        Path(prefix, "/Metadata/Status"),
                        resource);
                }

                if (HasEnteredInErrorFields(metadata))
                {
                    AddIssue(
                        issues,
                        ArchiveValidationCodes.InvalidLifecycleState,
                        ArchiveValidationSeverity.Error,
                        ArchiveValidationCategory.Integrity,
                        "草稿包含错误建立状态字段。",
                        Path(prefix, "/Metadata/Status"),
                        resource);
                }

                break;
            case ResourceLifecycleStatus.Final:
                ValidateFinalization(metadata, scope, prefix, resource, issues);
                if (metadata.Supersedes is not null || HasEnteredInErrorFields(metadata))
                {
                    AddIssue(
                        issues,
                        ArchiveValidationCodes.InvalidLifecycleState,
                        ArchiveValidationSeverity.Error,
                        ArchiveValidationCategory.Integrity,
                        "正式初始版本包含其他生命周期状态专用字段。",
                        Path(prefix, "/Metadata/Status"),
                        resource);
                }

                break;
            case ResourceLifecycleStatus.Amended:
                ValidateFinalization(metadata, scope, prefix, resource, issues);
                if (metadata.Supersedes is null)
                {
                    AddIssue(
                        issues,
                        ArchiveValidationCodes.InvalidRevisionRelationship,
                        FormalRequirementSeverity(scope),
                        ArchiveValidationCategory.Integrity,
                        "正式修订版本缺少被替代版本引用。",
                        Path(prefix, "/Metadata/Supersedes"),
                        resource);
                }

                if (HasEnteredInErrorFields(metadata))
                {
                    AddIssue(
                        issues,
                        ArchiveValidationCodes.InvalidLifecycleState,
                        ArchiveValidationSeverity.Error,
                        ArchiveValidationCategory.Integrity,
                        "正式修订版本包含错误建立状态字段。",
                        Path(prefix, "/Metadata/Status"),
                        resource);
                }

                break;
            case ResourceLifecycleStatus.EnteredInError:
                if (metadata.EnteredInErrorAt is null ||
                    metadata.EnteredInErrorBy is null ||
                    (metadata.EnteredInErrorReason is null && string.IsNullOrWhiteSpace(metadata.EnteredInErrorReasonText)))
                {
                    AddIssue(
                        issues,
                        ArchiveValidationCodes.InvalidLifecycleState,
                        FormalRequirementSeverity(scope),
                        ArchiveValidationCategory.Integrity,
                        "错误建立状态缺少时间、执行者或原因。",
                        Path(prefix, "/Metadata/Status"),
                        resource);
                }

                break;
        }

        ValidateActorReference(
            metadata.FinalizedBy,
            Path(prefix, "/Metadata/FinalizedBy"),
            resource,
            issues);
        ValidateActorReference(
            metadata.EnteredInErrorBy,
            Path(prefix, "/Metadata/EnteredInErrorBy"),
            resource,
            issues);

        if (metadata.Status is ResourceLifecycleStatus.Final or ResourceLifecycleStatus.Amended &&
            metadata.FinalizedAt is { } finalizedAt &&
            metadata.LastModifiedAt > finalizedAt)
        {
            AddIssue(
                issues,
                ArchiveValidationCodes.InvalidResourceTimeline,
                ArchiveValidationSeverity.Error,
                ArchiveValidationCategory.Integrity,
                "正式版本在确认时间之后仍记录了内容修改。",
                Path(prefix, "/Metadata/LastModifiedAt"),
                resource);
        }

        ValidateExtensions(metadata.Extensions, Path(prefix, "/Metadata/Extensions"), resource, issues);
    }

    private static void ValidateFinalization(
        ResourceMetadata metadata,
        ArchiveValidationScope scope,
        string prefix,
        IArchiveResource resource,
        ICollection<ArchiveValidationIssue> issues)
    {
        if (metadata.FinalizedAt is null || !HasActorIdentity(metadata.FinalizedBy))
        {
            AddIssue(
                issues,
                ArchiveValidationCodes.MissingFinalization,
                FormalRequirementSeverity(scope),
                ArchiveValidationCategory.Integrity,
                "正式资源缺少确认时间或确认者说明。",
                Path(prefix, "/Metadata"),
                resource);
        }

        if (metadata.BasedOn is not null)
        {
            AddIssue(
                issues,
                ArchiveValidationCodes.InvalidRevisionRelationship,
                FormalRequirementSeverity(scope),
                ArchiveValidationCategory.Integrity,
                "正式资源不应继续使用草稿编辑来源关系。",
                Path(prefix, "/Metadata/BasedOn"),
                resource);
        }
    }

    private static void ValidatePatient(
        PatientResource patient,
        ArchiveValidationScope scope,
        string prefix,
        ICollection<ArchiveValidationIssue> issues)
    {
        ValidateActorReference(
            patient.ManagingOrganization,
            Path(prefix, "/ManagingOrganization"),
            patient,
            issues);

        if (patient.IdentityMode == PatientIdentityMode.Identified &&
            patient.Names.Count == 0 &&
            patient.BusinessIdentifiers.Count == 0)
        {
            AddIssue(
                issues,
                ArchiveValidationCodes.RequiredSemanticValueMissing,
                FormalRequirementSeverity(scope),
                ArchiveValidationCategory.Integrity,
                "已识别身份模式缺少姓名或业务标识。",
                Path(prefix, "/IdentityMode"),
                patient);
        }

        if (patient.IdentityMode == PatientIdentityMode.Unlinked && patient.BusinessIdentifiers.Count > 0)
        {
            AddIssue(
                issues,
                ArchiveValidationCodes.InvalidLifecycleState,
                ArchiveValidationSeverity.Warning,
                ArchiveValidationCategory.Integrity,
                "未关联身份模式包含外部业务标识，请复核身份模式。",
                Path(prefix, "/BusinessIdentifiers"),
                patient);
        }
    }

    private static void ValidateConsultation(
        ConsultationResource consultation,
        string prefix,
        ICollection<ArchiveValidationIssue> issues)
    {
        ValidateActorReference(
            consultation.ServiceProvider,
            Path(prefix, "/ServiceProvider"),
            consultation,
            issues);

        if (consultation.SubjectReference.ExpectedResourceType is { } expectedType &&
            expectedType != ArchiveResourceTypes.Patient)
        {
            AddIssue(
                issues,
                ArchiveValidationCodes.ReferenceTypeMismatch,
                ArchiveValidationSeverity.Error,
                ArchiveValidationCategory.Integrity,
                "咨询对象引用声明的资源类型不是 Patient。",
                Path(prefix, "/SubjectReference"),
                consultation);
        }

        ValidateDistinctVersionedReferences(
            consultation.ClinicalResourceReferences,
            Path(prefix, "/ClinicalResourceReferences"),
            consultation,
            issues);

        var snapshot = consultation.SubjectSnapshot;
        if (snapshot is null)
        {
            return;
        }

        ValidateDistinctCodings(
            snapshot.PhysiologicalStates,
            Path(prefix, "/SubjectSnapshot/PhysiologicalStates"),
            consultation,
            issues);
    }

    private static void ValidateEnergyAssessment(
        EnergyAssessmentResource energy,
        ArchiveValidationScope scope,
        string prefix,
        ICollection<ArchiveValidationIssue> issues)
    {
        ValidateAssessmentReferenceTypes(energy.SubjectReference, energy.ConsultationReference, prefix, energy, issues);
        ValidateUniqueLocalIds(
            energy.CandidateCalculations.Select(candidate => candidate.CandidateId),
            Path(prefix, "/CandidateCalculations"),
            energy,
            issues);

        for (var index = 0; index < energy.CandidateCalculations.Count; index++)
        {
            var candidate = energy.CandidateCalculations[index];
            var candidatePath = Path(prefix, $"/CandidateCalculations/{index}");
            ValidateDistinctAssessmentInputs(candidate.Inputs, candidatePath + "/Inputs", energy, issues);
            foreach (var referenceData in candidate.ReferenceData)
            {
                ValidateReferenceData(referenceData, scope, candidatePath + "/ReferenceData", energy, issues);
            }

        }

        ValidateValueAbsentReasonChoice(
            energy.ProfessionalDecision,
            energy.ProfessionalDecisionAbsentReason,
            scope,
            Path(prefix, "/ProfessionalDecision"),
            energy,
            issues);

        var decision = energy.ProfessionalDecision;
        if (decision is not null)
        {
            ValidateValueAbsentReasonChoice(
                decision.DecisionBasis,
                decision.DecisionBasisAbsentReason,
                scope,
                Path(prefix, "/ProfessionalDecision/DecisionBasis"),
                energy,
                issues);

            if (decision.SelectedCandidateId is { } selectedId)
            {
                var candidate = energy.CandidateCalculations.SingleOrDefault(item => item.CandidateId == selectedId);
                if (candidate is null)
                {
                    AddIssue(
                        issues,
                        ArchiveValidationCodes.UnresolvedReference,
                        ArchiveValidationSeverity.Error,
                        ArchiveValidationCategory.Integrity,
                        "专业决定引用了不存在的候选计算。",
                        Path(prefix, "/ProfessionalDecision/SelectedCandidateId"),
                        energy);
                }
                else if (!QuantitiesEqual(candidate.Result, decision.AdoptedEnergyTarget) &&
                         string.IsNullOrWhiteSpace(decision.Reason))
                {
                    AddIssue(
                        issues,
                        ArchiveValidationCodes.AdjustmentReasonMissing,
                        FormalRequirementSeverity(scope),
                        ArchiveValidationCategory.Integrity,
                        "专业采用值与候选结果不同但缺少调整说明。",
                        Path(prefix, "/ProfessionalDecision/Reason"),
                        energy);
                }
            }
        }

        if (energy.AllocationPlan is { } allocation)
        {
            ValidateEnergyAllocation(allocation, decision, prefix, energy, issues);
        }
    }

    private static void ValidateEnergyAllocation(
        EnergyAllocationPlan allocation,
        ProfessionalEnergyDecision? decision,
        string prefix,
        EnergyAssessmentResource resource,
        ICollection<ArchiveValidationIssue> issues)
    {
        var allocationPath = Path(prefix, "/AllocationPlan");
        if (decision is not null && !QuantitiesEqual(allocation.EnergyTarget, decision.AdoptedEnergyTarget))
        {
            AddIssue(
                issues,
                ArchiveValidationCodes.NutrientAggregationMismatch,
                ArchiveValidationSeverity.Error,
                ArchiveValidationCategory.Integrity,
                "能量分配方案与专业采用能量不一致。",
                allocationPath + "/EnergyTarget",
                resource);
        }

        ValidateDistinctCodings(
            allocation.MacronutrientTargets.Select(target => target.Nutrient),
            allocationPath + "/MacronutrientTargets",
            resource,
            issues);
        var fractionSum = allocation.MacronutrientTargets.Sum(target => target.EnergyFraction);
        if (allocation.MacronutrientTargets.Any(target => target.EnergyFraction is < 0m or > 1m) ||
            (allocation.MacronutrientTargets.Count > 0 && Math.Abs(fractionSum - 1m) > 0.000001m))
        {
            AddIssue(
                issues,
                ArchiveValidationCodes.InvalidTechnicalValue,
                ArchiveValidationSeverity.Error,
                ArchiveValidationCategory.Integrity,
                "宏量营养素供能比例不在定义范围内或合计不为 1。",
                allocationPath + "/MacronutrientTargets",
                resource);
        }

        for (var index = 0; index < allocation.MacronutrientTargets.Count; index++)
        {
            var target = allocation.MacronutrientTargets[index];
            ValidateDistinctCodings(
                target.MealAllocations.Select(meal => meal.MealOccasion),
                $"{allocationPath}/MacronutrientTargets/{index}/MealAllocations",
                resource,
                issues);
            if (target.MealAllocations.Count > 0)
            {
                var unitsMatch = target.MealAllocations.All(meal =>
                    AreDailyTargetAndMealUnitsCompatible(target.DailyAmount.Unit, meal.Amount.Unit));
                if (!unitsMatch)
                {
                    AddIssue(
                        issues,
                        ArchiveValidationCodes.IncompatibleUnits,
                        ArchiveValidationSeverity.Error,
                        ArchiveValidationCategory.Integrity,
                        "宏量营养素每日目标与餐次目标单位不一致。",
                        $"{allocationPath}/MacronutrientTargets/{index}",
                        resource);
                }
                else if (target.DailyAmount.Value != target.MealAllocations.Sum(meal => meal.Amount.Value))
                {
                    AddIssue(
                        issues,
                        ArchiveValidationCodes.NutrientAggregationMismatch,
                        ArchiveValidationSeverity.Error,
                        ArchiveValidationCategory.Integrity,
                        "宏量营养素餐次目标之和与每日目标不一致。",
                        $"{allocationPath}/MacronutrientTargets/{index}",
                        resource);
                }
            }
        }

        ValidateDistinctCodings(
            allocation.FoodExchangeTargets.Select(target => target.FoodGroup),
            allocationPath + "/FoodExchangeTargets",
            resource,
            issues);
    }

    private static void ValidateDriAssessment(
        DriAssessmentResource dri,
        ArchiveValidationScope scope,
        string prefix,
        ICollection<ArchiveValidationIssue> issues)
    {
        ValidateAssessmentReferenceTypes(dri.SubjectReference, dri.ConsultationReference, prefix, dri, issues);
        ValidateDistinctAssessmentInputs(dri.InputContext, Path(prefix, "/InputContext"), dri, issues);
        ValidateValueAbsentReasonChoice(
            dri.ReferenceData,
            dri.ReferenceDataAbsentReason,
            scope,
            Path(prefix, "/ReferenceData"),
            dri,
            issues);
        if (dri.ReferenceData is { } referenceData)
        {
            ValidateReferenceData(referenceData, scope, Path(prefix, "/ReferenceData"), dri, issues);
        }

        ValidateDistinctCodings(
            dri.NutrientResults.Select(result => result.Nutrient),
            Path(prefix, "/NutrientResults"),
            dri,
            issues);
        for (var nutrientIndex = 0; nutrientIndex < dri.NutrientResults.Count; nutrientIndex++)
        {
            var result = dri.NutrientResults[nutrientIndex];
            var resultPath = Path(prefix, $"/NutrientResults/{nutrientIndex}");
            ValidateDistinctCodings(
                result.ReferenceValues.Select(value => value.ReferenceType),
                resultPath + "/ReferenceValues",
                dri,
                issues);
            for (var valueIndex = 0; valueIndex < result.ReferenceValues.Count; valueIndex++)
            {
                ValidateDriReferenceValue(
                    result.ReferenceValues[valueIndex],
                    scope,
                    $"{resultPath}/ReferenceValues/{valueIndex}",
                    dri,
                    issues);
            }
        }
    }

    private static void ValidateDriReferenceValue(
        DriReferenceValue value,
        ArchiveValidationScope scope,
        string path,
        DriAssessmentResource resource,
        ICollection<ArchiveValidationIssue> issues)
    {
        ValidateValueAbsentReasonChoice(value.AdoptedValue, value.AbsentReason, scope, path + "/AdoptedValue", resource, issues);
        if (value.BasisValue is not null && value.AdoptedValue is not null &&
            value.BasisValue != value.AdoptedValue &&
            string.IsNullOrWhiteSpace(value.AdjustmentReason))
        {
            AddIssue(
                issues,
                ArchiveValidationCodes.AdjustmentReasonMissing,
                FormalRequirementSeverity(scope),
                ArchiveValidationCategory.Integrity,
                "DRIs 基础值与采用值不同但缺少调整说明。",
                path + "/AdjustmentReason",
                resource);
        }

        var absoluteComponents = value.Components.Where(component => !component.IsOffset).ToArray();
        if (absoluteComponents.Length != 1 || value.Components.Count == 0)
        {
            return;
        }

        var unit = absoluteComponents[0].Value.Unit;
        if (!value.Components.All(component => component.Value.Unit.HasSameIdentity(unit)))
        {
            AddIssue(
                issues,
                ArchiveValidationCodes.IncompatibleUnits,
                ArchiveValidationSeverity.Error,
                ArchiveValidationCategory.Integrity,
                "DRIs 基础值和偏移分量单位不一致。",
                path + "/Components",
                resource);
            return;
        }

        var calculated = new Quantity(value.Components.Sum(component => component.Value.Value), unit);
        if (value.BasisValue is QuantityArchiveValue basis && !QuantitiesEqual(basis.Value, calculated))
        {
            AddIssue(
                issues,
                ArchiveValidationCodes.NutrientAggregationMismatch,
                ArchiveValidationSeverity.Error,
                ArchiveValidationCategory.Integrity,
                "DRIs 基础值无法由保存的分量复核。",
                path + "/BasisValue",
                resource);
        }
    }

    private static void ValidateDietaryRecall(
        DietaryRecallResource recall,
        ArchiveValidationScope scope,
        string prefix,
        ICollection<ArchiveValidationIssue> issues)
    {
        ValidateAssessmentReferenceTypes(recall.SubjectReference, recall.ConsultationReference, prefix, recall, issues);
        ValidateValueAbsentReasonChoice(
            recall.RecallPeriod,
            recall.RecallPeriodAbsentReason,
            scope,
            Path(prefix, "/RecallPeriod"),
            recall,
            issues);

        if (recall.Status is null && recall.Metadata.Status != ResourceLifecycleStatus.Draft)
        {
            AddIssue(
                issues,
                ArchiveValidationCodes.RequiredSemanticValueMissing,
                FormalRequirementSeverity(scope),
                ArchiveValidationCategory.Integrity,
                "正式膳食回忆缺少摄入状态。",
                Path(prefix, "/Status"),
                recall);
        }

        var entries = recall.Meals.SelectMany(meal => meal.Entries).ToArray();
        if (recall.Status == DietaryRecallStatus.IntakeReported && entries.Length == 0)
        {
            AddIssue(
                issues,
                ArchiveValidationCodes.RequiredSemanticValueMissing,
                ArchiveValidationSeverity.Error,
                ArchiveValidationCategory.Integrity,
                "摄入已报告状态缺少食物条目。",
                Path(prefix, "/Meals"),
                recall);
        }

        if (recall.Status == DietaryRecallStatus.NoIntake &&
            (entries.Length > 0 || recall.TotalNutrientSummary.Any(value => value.Amount.Value != 0)))
        {
            AddIssue(
                issues,
                ArchiveValidationCodes.InvalidLifecycleState,
                ArchiveValidationSeverity.Error,
                ArchiveValidationCategory.Integrity,
                "明确未摄入状态与食物条目或非零汇总并存。",
                Path(prefix, "/Status"),
                recall);
        }

        ValidateUniqueLocalIds(recall.Meals.Select(meal => meal.MealId), Path(prefix, "/Meals"), recall, issues);
        ValidateUniqueLocalIds(entries.Select(entry => entry.EntryId), Path(prefix, "/Meals"), recall, issues);
        ValidatePositiveUniqueSequences(recall.Meals.Select(meal => meal.Sequence), Path(prefix, "/Meals"), recall, issues);

        for (var mealIndex = 0; mealIndex < recall.Meals.Count; mealIndex++)
        {
            var meal = recall.Meals[mealIndex];
            var mealPath = Path(prefix, $"/Meals/{mealIndex}");
            ValidatePositiveUniqueSequences(meal.Entries.Select(entry => entry.Sequence), mealPath + "/Entries", recall, issues);
            for (var entryIndex = 0; entryIndex < meal.Entries.Count; entryIndex++)
            {
                var entry = meal.Entries[entryIndex];
                var entryPath = $"{mealPath}/Entries/{entryIndex}";
                if (entry.EdibleFraction is < 0m or > 1m)
                {
                    AddIssue(
                        issues,
                        ArchiveValidationCodes.InvalidTechnicalValue,
                        ArchiveValidationSeverity.Error,
                        ArchiveValidationCategory.Integrity,
                        "可食比例必须位于 0 至 1 之间。",
                        entryPath + "/EdibleFraction",
                        recall);
                }

                ValidateValueAbsentReasonChoice(
                    entry.FoodCompositionData,
                    entry.FoodCompositionDataAbsentReason,
                    scope,
                    entryPath + "/FoodCompositionData",
                    recall,
                    issues);
                if (entry.FoodCompositionData is { } foodData)
                {
                    ValidateReferenceData(foodData, scope, entryPath + "/FoodCompositionData", recall, issues);
                }

                ValidateNutrientList(entry.NutrientContributions, entryPath + "/NutrientContributions", recall, issues);
            }

            ValidateNutrientList(meal.NutrientSummary, mealPath + "/NutrientSummary", recall, issues);
            if (meal.NutrientSummary.Count > 0)
            {
                CompareNutrientAggregation(
                    meal.Entries.SelectMany(entry => entry.NutrientContributions),
                    meal.NutrientSummary,
                    mealPath + "/NutrientSummary",
                    recall,
                    issues);
            }
        }

        ValidateNutrientList(recall.TotalNutrientSummary, Path(prefix, "/TotalNutrientSummary"), recall, issues);
        if (recall.TotalNutrientSummary.Count > 0)
        {
            CompareNutrientAggregation(
                recall.Meals.SelectMany(meal => meal.NutrientSummary),
                recall.TotalNutrientSummary,
                Path(prefix, "/TotalNutrientSummary"),
                recall,
                issues);
        }

        if (recall.EnergyConsistency is { } consistency)
        {
            ValidateDietaryEnergyConsistency(consistency, recall, prefix, issues);
        }

        if (recall.GuidanceSnapshot is { } guidance)
        {
            ValidateValueAbsentReasonChoice(
                guidance.Guideline,
                guidance.GuidelineAbsentReason,
                scope,
                Path(prefix, "/GuidanceSnapshot/Guideline"),
                recall,
                issues);
            if (guidance.Guideline is { } guideline)
            {
                ValidateReferenceData(guideline, scope, Path(prefix, "/GuidanceSnapshot/Guideline"), recall, issues);
            }
        }
    }

    private static void ValidateDietaryEnergyConsistency(
        DietaryEnergyConsistency consistency,
        DietaryRecallResource recall,
        string prefix,
        ICollection<ArchiveValidationIssue> issues)
    {
        var path = Path(prefix, "/EnergyConsistency");
        if (!consistency.RecordedTotalEnergy.Unit.HasSameIdentity(consistency.MacronutrientDerivedEnergy.Unit))
        {
            AddIssue(
                issues,
                ArchiveValidationCodes.IncompatibleUnits,
                ArchiveValidationSeverity.Error,
                ArchiveValidationCategory.Integrity,
                "记录总能量与宏量营养素折算能量单位不一致。",
                path,
                recall);
            return;
        }

        if (consistency.AllowedDifference is not null && consistency.AllowedDifferenceAbsentReason is not null)
        {
            AddIssue(
                issues,
                ArchiveValidationCodes.ValueAndAbsentReasonConflict,
                ArchiveValidationSeverity.Error,
                ArchiveValidationCategory.Integrity,
                "能量容差与其缺失原因不能同时存在。",
                path + "/AllowedDifference",
                recall);
        }

        if (consistency.AllowedDifference is { } allowedDifference &&
            (!allowedDifference.Unit.HasSameIdentity(consistency.RecordedTotalEnergy.Unit) || allowedDifference.Value < 0))
        {
            AddIssue(
                issues,
                allowedDifference.Value < 0
                    ? ArchiveValidationCodes.InvalidTechnicalValue
                    : ArchiveValidationCodes.IncompatibleUnits,
                ArchiveValidationSeverity.Error,
                ArchiveValidationCategory.Integrity,
                "能量容差的数值或单位无效。",
                path + "/AllowedDifference",
                recall);
            return;
        }

        var energy = FindNutrient(recall.TotalNutrientSummary, "energy");
        if (energy is not null && !QuantitiesEqual(energy.Amount, consistency.RecordedTotalEnergy))
        {
            AddIssue(
                issues,
                ArchiveValidationCodes.NutrientAggregationMismatch,
                ArchiveValidationSeverity.Error,
                ArchiveValidationCategory.Integrity,
                "全日能量汇总与一致性快照中的记录能量不同。",
                path + "/RecordedTotalEnergy",
                recall);
        }

        var protein = FindNutrient(recall.TotalNutrientSummary, "protein");
        var fat = FindNutrient(recall.TotalNutrientSummary, "total-fat") ??
            FindNutrient(recall.TotalNutrientSummary, "fat");
        var carbohydrate = FindNutrient(recall.TotalNutrientSummary, "carbohydrate");
        if (protein is null || fat is null || carbohydrate is null ||
            !protein.Amount.Unit.HasSameIdentity(fat.Amount.Unit) ||
            !protein.Amount.Unit.HasSameIdentity(carbohydrate.Amount.Unit))
        {
            return;
        }

        if (consistency.Method.Method.Code is "atwater-general-factors" or "synthetic-macro-energy")
        {
            if (!IsUcumGram(protein.Amount.Unit) ||
                !IsUcumGram(fat.Amount.Unit) ||
                !IsUcumGram(carbohydrate.Amount.Unit))
            {
                AddIssue(
                    issues,
                    ArchiveValidationCodes.IncompatibleUnits,
                    ArchiveValidationSeverity.Error,
                    ArchiveValidationCategory.Integrity,
                    "通用宏量营养素能量折算要求使用克作为输入单位。",
                    path + "/MacronutrientDerivedEnergy",
                    recall);
                return;
            }

            var calculated = (protein.Amount.Value * 4m) +
                (fat.Amount.Value * 9m) +
                (carbohydrate.Amount.Value * 4m);
            if (calculated != consistency.MacronutrientDerivedEnergy.Value)
            {
                AddIssue(
                    issues,
                    ArchiveValidationCodes.NutrientAggregationMismatch,
                    ArchiveValidationSeverity.Error,
                    ArchiveValidationCategory.Integrity,
                    "保存的宏量营养素折算能量无法由全日汇总复核。",
                    path + "/MacronutrientDerivedEnergy",
                    recall);
            }
        }
    }

    private static void ValidateNutritionAdvice(
        NutritionAdviceResource advice,
        ArchiveValidationScope scope,
        string prefix,
        ICollection<ArchiveValidationIssue> issues)
    {
        ValidateAssessmentReferenceTypes(advice.SubjectReference, advice.ConsultationReference, prefix, advice, issues);
        ValidateDistinctVersionedReferences(
            advice.InputResourceReferences,
            Path(prefix, "/InputResourceReferences"),
            advice,
            issues);
        ValidateDistinctNamedValues(advice.InputSummary, Path(prefix, "/InputSummary"), advice, issues);

        var invalidTimeline = advice.RequestedAt is { } requested &&
            advice.CompletedAt is { } completed && completed < requested;
        var stateInvalid = advice.GenerationStatus switch
        {
            NutritionAdviceGenerationStatus.Prepared => advice.CompletedAt is not null,
            NutritionAdviceGenerationStatus.Generating => advice.RequestedAt is null || advice.CompletedAt is not null,
            NutritionAdviceGenerationStatus.Completed =>
                advice.RequestedAt is null || advice.CompletedAt is null || string.IsNullOrWhiteSpace(advice.NarrativeContent),
            NutritionAdviceGenerationStatus.Incomplete => advice.RequestedAt is null,
            NutritionAdviceGenerationStatus.Failed => advice.RequestedAt is null,
            _ => true
        };
        if (invalidTimeline || stateInvalid)
        {
            AddIssue(
                issues,
                ArchiveValidationCodes.AdviceGenerationStateInvalid,
                advice.Metadata.Status == ResourceLifecycleStatus.Draft
                    ? ArchiveValidationSeverity.Warning
                    : FormalRequirementSeverity(scope),
                ArchiveValidationCategory.Integrity,
                "营养建议的生成状态、时间或正文不一致。",
                Path(prefix, "/GenerationStatus"),
                advice);
        }
    }

    private static void ValidateNutritionReport(
        NutritionReportResource report,
        ArchiveValidationScope scope,
        string prefix,
        ICollection<ArchiveValidationIssue> issues)
    {
        ValidateAssessmentReferenceTypes(
            report.SubjectReference,
            report.ConsultationReference,
            prefix,
            report,
            issues);
        ValidateDistinctVersionedReferences(
            report.InputResourceReferences,
            Path(prefix, "/InputResourceReferences"),
            report,
            issues);

        for (var index = 0; index < report.InputResourceReferences.Count; index++)
        {
            if (IsSelfReference(report.InputResourceReferences[index], report.Metadata))
            {
                AddIssue(
                    issues,
                    ArchiveValidationCodes.InvalidTechnicalValue,
                    ArchiveValidationSeverity.Error,
                    ArchiveValidationCategory.Integrity,
                    "报告不能把自身当前版本声明为内容输入。",
                    $"{prefix}/InputResourceReferences/{index}",
                    report);
            }
        }

        if (report.Participants.Count == 0)
        {
            AddIssue(
                issues,
                ArchiveValidationCodes.RequiredSemanticValueMissing,
                FormalRequirementSeverity(scope),
                ArchiveValidationCategory.Integrity,
                "营养报告缺少作者、复核者或监督者等参与事实。",
                Path(prefix, "/Participants"),
                report);
        }

        for (var index = 0; index < report.Participants.Count; index++)
        {
            var participant = report.Participants[index];
            var participantPath = $"{prefix}/Participants/{index}";
            ValidateActorReference(
                participant.Actor,
                participantPath + "/Actor",
                report,
                issues);
            if (participant.ActedAt is { } actedAt && actedAt > report.Metadata.LastModifiedAt)
            {
                AddIssue(
                    issues,
                    ArchiveValidationCodes.InvalidResourceTimeline,
                    ArchiveValidationSeverity.Error,
                    ArchiveValidationCategory.Integrity,
                    "报告参与时间晚于当前资源版本的最后修改时间。",
                    participantPath + "/ActedAt",
                    report);
            }
        }

        if (report.Metadata.Status is ResourceLifecycleStatus.Final or ResourceLifecycleStatus.Amended &&
            report.RenderedArtifact is null)
        {
            // 正式签发绑定的是用户实际看到的产物，而不只是可能被重新渲染的输入与模板。
            AddIssue(
                issues,
                ArchiveValidationCodes.RequiredSemanticValueMissing,
                FormalRequirementSeverity(scope),
                ArchiveValidationCategory.Integrity,
                "正式营养报告缺少用于绑定确切输出内容的渲染产物指纹。",
                Path(prefix, "/RenderedArtifact"),
                report);
        }
    }

    private static void ValidateNutritionScaleAssessment(
        NutritionScaleAssessmentResource scale,
        ArchiveValidationScope scope,
        string prefix,
        ICollection<ArchiveValidationIssue> issues)
    {
        ValidateAssessmentReferenceTypes(
            scale.SubjectReference,
            scale.ConsultationReference,
            prefix,
            scale,
            issues);
        ValidateDistinctVersionedReferences(
            scale.InputResourceReferences,
            Path(prefix, "/InputResourceReferences"),
            scale,
            issues);

        for (var index = 0; index < scale.InputResourceReferences.Count; index++)
        {
            if (IsSelfReference(scale.InputResourceReferences[index], scale.Metadata))
            {
                AddIssue(
                    issues,
                    ArchiveValidationCodes.InvalidTechnicalValue,
                    ArchiveValidationSeverity.Error,
                    ArchiveValidationCategory.Integrity,
                    "量表评估不能把自身当前版本声明为评分输入。",
                    $"{prefix}/InputResourceReferences/{index}",
                    scale);
            }
        }

        var instrument = scale.Instrument;
        if (instrument.Version is null &&
            instrument.Definition?.Version is null &&
            instrument.DefinitionFingerprint is null)
        {
            AddIssue(
                issues,
                ArchiveValidationCodes.AssessmentInstrumentIdentityIncomplete,
                scale.Metadata.Status == ResourceLifecycleStatus.Draft
                    ? ArchiveValidationSeverity.Warning
                    : FormalRequirementSeverity(scope),
                ArchiveValidationCategory.Compatibility,
                "量表身份缺少编码版本、规范版本或定义内容指纹，无法稳定解释历史回答。",
                Path(prefix, "/Instrument"),
                scale);
        }

        if (scale.Responses.Count == 0 && scale.Metadata.Status != ResourceLifecycleStatus.Draft)
        {
            AddIssue(
                issues,
                ArchiveValidationCodes.RequiredSemanticValueMissing,
                FormalRequirementSeverity(scope),
                ArchiveValidationCategory.Integrity,
                "正式量表评估缺少题目回答快照。",
                Path(prefix, "/Responses"),
                scale);
        }

        ValidateDistinctCodings(
            scale.Responses.Select(response => response.Item),
            Path(prefix, "/Responses"),
            scale,
            issues);
        for (var index = 0; index < scale.Responses.Count; index++)
        {
            var response = scale.Responses[index];
            ValidateValueAbsentReasonChoice(
                response.Answer,
                response.AnswerAbsentReason,
                scope,
                $"{prefix}/Responses/{index}",
                scale,
                issues);
        }

        ValidateDistinctCodings(
            scale.DerivedResults.Select(result => result.Name),
            Path(prefix, "/DerivedResults"),
            scale,
            issues);

        if (scale.TotalScore is not null && scale.TotalScoreAbsentReason is not null)
        {
            AddIssue(
                issues,
                ArchiveValidationCodes.ValueAndAbsentReasonConflict,
                ArchiveValidationSeverity.Error,
                ArchiveValidationCategory.Integrity,
                "量表总分与其缺失原因不能同时存在。",
                Path(prefix, "/TotalScore"),
                scale);
        }
        else if (scale.TotalScore is null &&
                 scale.TotalScoreAbsentReason is null &&
                 scale.Metadata.Status != ResourceLifecycleStatus.Draft)
        {
            AddIssue(
                issues,
                ArchiveValidationCodes.RequiredSemanticValueMissing,
                FormalRequirementSeverity(scope),
                ArchiveValidationCategory.Integrity,
                "正式量表评估缺少总分及其明确缺失原因。",
                Path(prefix, "/TotalScore"),
                scale);
        }

        ValidateActorReference(
            scale.Performer,
            Path(prefix, "/Performer"),
            scale,
            issues);
    }

    private static void ValidateBundleReferences(
        ArchiveBundle bundle,
        ICollection<ArchiveValidationIssue> issues)
    {
        var entries = bundle.Entries;
        for (var index = 0; index < entries.Count; index++)
        {
            var resource = entries[index];
            var prefix = $"/Entries/{index}";
            ValidateOptionalVersionedReference(resource.Metadata.BasedOn, prefix + "/Metadata/BasedOn", resource, entries, issues);
            ValidateOptionalVersionedReference(resource.Metadata.Supersedes, prefix + "/Metadata/Supersedes", resource, entries, issues);
            switch (resource)
            {
                case ConsultationResource consultation:
                    ValidateLogicalReference(consultation.SubjectReference, prefix + "/SubjectReference", resource, entries, issues);
                    for (var referenceIndex = 0; referenceIndex < consultation.ClinicalResourceReferences.Count; referenceIndex++)
                    {
                        ValidateVersionedReference(
                            consultation.ClinicalResourceReferences[referenceIndex],
                            $"{prefix}/ClinicalResourceReferences/{referenceIndex}",
                            resource,
                            entries,
                            issues);
                    }

                    break;
                case EnergyAssessmentResource energy:
                    ValidateAssessmentReferences(energy.SubjectReference, energy.ConsultationReference, prefix, resource, entries, issues);
                    break;
                case DriAssessmentResource dri:
                    ValidateAssessmentReferences(dri.SubjectReference, dri.ConsultationReference, prefix, resource, entries, issues);
                    break;
                case DietaryRecallResource recall:
                    ValidateAssessmentReferences(recall.SubjectReference, recall.ConsultationReference, prefix, resource, entries, issues);
                    break;
                case SoapNoteResource soap:
                    ValidateAssessmentReferences(soap.SubjectReference, soap.ConsultationReference, prefix, resource, entries, issues);
                    break;
                case NutritionAdviceResource advice:
                    ValidateAssessmentReferences(advice.SubjectReference, advice.ConsultationReference, prefix, resource, entries, issues);
                    for (var referenceIndex = 0; referenceIndex < advice.InputResourceReferences.Count; referenceIndex++)
                    {
                        ValidateVersionedReference(
                            advice.InputResourceReferences[referenceIndex],
                            $"{prefix}/InputResourceReferences/{referenceIndex}",
                            resource,
                            entries,
                            issues);
                    }

                    break;
                case NutritionReportResource report:
                    ValidateAssessmentReferences(
                        report.SubjectReference,
                        report.ConsultationReference,
                        prefix,
                        resource,
                        entries,
                        issues);
                    for (var referenceIndex = 0; referenceIndex < report.InputResourceReferences.Count; referenceIndex++)
                    {
                        ValidateVersionedReference(
                            report.InputResourceReferences[referenceIndex],
                            $"{prefix}/InputResourceReferences/{referenceIndex}",
                            resource,
                            entries,
                            issues);
                    }

                    break;
                case NutritionScaleAssessmentResource scale:
                    ValidateAssessmentReferences(
                        scale.SubjectReference,
                        scale.ConsultationReference,
                        prefix,
                        resource,
                        entries,
                        issues);
                    for (var referenceIndex = 0; referenceIndex < scale.InputResourceReferences.Count; referenceIndex++)
                    {
                        ValidateVersionedReference(
                            scale.InputResourceReferences[referenceIndex],
                            $"{prefix}/InputResourceReferences/{referenceIndex}",
                            resource,
                            entries,
                            issues);
                    }

                    break;
            }
        }
    }

    private static void ValidateConsultationDocument(
        ArchiveBundle bundle,
        ICollection<ArchiveValidationIssue> issues)
    {
        if (bundle.BundleType != ArchiveBundleType.ConsultationDocument)
        {
            return;
        }

        var consultations = bundle.Entries.OfType<ConsultationResource>().ToArray();
        var patients = bundle.Entries.OfType<PatientResource>().ToArray();
        if (consultations.Length != 1 || patients.Length != 1)
        {
            AddIssue(
                issues,
                ArchiveValidationCodes.ConsultationClosureMismatch,
                ArchiveValidationSeverity.Error,
                ArchiveValidationCategory.Integrity,
                "咨询文档必须恰好包含一个 Consultation 和一个 Patient。",
                "/Entries",
                null);
            return;
        }

        var consultation = consultations[0];
        var patient = patients[0];
        var clinicalResources = bundle.Entries
            .Where(resource => resource is not PatientResource and not ConsultationResource)
            .ToArray();
        var referencedKeys = consultation.ClinicalResourceReferences
            .Select(VersionKey)
            .ToHashSet(StringComparer.Ordinal);
        var actualKeys = clinicalResources
            .Select(resource => VersionKey(resource.Metadata.ResourceId, resource.Metadata.VersionId))
            .ToHashSet(StringComparer.Ordinal);
        if (!referencedKeys.SetEquals(actualKeys))
        {
            AddIssue(
                issues,
                ArchiveValidationCodes.ConsultationClosureMismatch,
                ArchiveValidationSeverity.Error,
                ArchiveValidationCategory.Integrity,
                "咨询组成资源清单与 Bundle 中的临床资源不一致。",
                "/Entries",
                consultation);
        }

        foreach (var resource in clinicalResources)
        {
            var subject = GetSubjectReference(resource);
            if (subject is not null && subject.ResourceId != patient.Metadata.ResourceId)
            {
                AddIssue(
                    issues,
                    ArchiveValidationCodes.SubjectReferenceMismatch,
                    ArchiveValidationSeverity.Error,
                    ArchiveValidationCategory.Integrity,
                    "咨询组成资源引用了不同的咨询对象。",
                    "/Entries",
                    resource);
            }

            var consultationReference = GetConsultationReference(resource);
            if (IsKnownConsultationResource(resource) &&
                (consultationReference is null ||
                 consultationReference.ResourceId != consultation.Metadata.ResourceId ||
                 consultationReference.VersionId != consultation.Metadata.VersionId))
            {
                AddIssue(
                    issues,
                    ArchiveValidationCodes.ConsultationClosureMismatch,
                    ArchiveValidationSeverity.Error,
                    ArchiveValidationCategory.Integrity,
                    "咨询组成资源没有反向引用当前 Consultation 版本。",
                    "/Entries",
                    resource);
            }
        }
    }

    private static void ValidateRevisionGraph(
        IReadOnlyList<IArchiveResource> entries,
        ICollection<ArchiveValidationIssue> issues)
    {
        foreach (var resource in entries)
        {
            var predecessorReference = resource.Metadata.Supersedes;
            if (predecessorReference is null)
            {
                continue;
            }

            var predecessor = FindVersion(entries, predecessorReference);
            if (predecessor is null)
            {
                continue;
            }

            if (predecessor.Metadata.ResourceId != resource.Metadata.ResourceId ||
                predecessor.ResourceType != resource.ResourceType ||
                predecessor.Metadata.RevisionNumber.Value >= resource.Metadata.RevisionNumber.Value)
            {
                AddIssue(
                    issues,
                    ArchiveValidationCodes.InvalidRevisionRelationship,
                    ArchiveValidationSeverity.Error,
                    ArchiveValidationCategory.Integrity,
                    "正式修订的前驱资源身份、类型或修订序号无效。",
                    "/Entries",
                    resource);
            }
        }

        foreach (var group in entries.GroupBy(resource => resource.Metadata.ResourceId))
        {
            var versions = group.ToArray();
            if (versions.Length < 2)
            {
                continue;
            }

            var supersededVersions = versions
                .Select(resource => resource.Metadata.Supersedes)
                .Where(reference => reference is not null && reference.ResourceId == group.Key)
                .Select(reference => reference!.VersionId)
                .ToHashSet();
            var heads = versions.Count(resource => !supersededVersions.Contains(resource.Metadata.VersionId));
            if (heads > 1)
            {
                AddIssue(
                    issues,
                    ArchiveValidationCodes.ConcurrentVersionHeads,
                    ArchiveValidationSeverity.Warning,
                    ArchiveValidationCategory.Integrity,
                    "同一逻辑资源存在多个未解决的当前版本头。",
                    "/Entries",
                    versions[0]);
            }

            foreach (var start in versions)
            {
                var visited = new HashSet<ResourceVersionId>();
                var current = start;
                while (current.Metadata.Supersedes is { } predecessorReference)
                {
                    if (!visited.Add(current.Metadata.VersionId))
                    {
                        AddIssue(
                            issues,
                            ArchiveValidationCodes.InvalidRevisionRelationship,
                            ArchiveValidationSeverity.Error,
                            ArchiveValidationCategory.Integrity,
                            "资源修订关系形成循环。",
                            "/Entries",
                            start);
                        break;
                    }

                    var predecessor = FindVersion(versions, predecessorReference);
                    if (predecessor is null)
                    {
                        break;
                    }

                    current = predecessor;
                }
            }
        }
    }

    private static void ValidateUniqueResourceVersions(
        IReadOnlyList<IArchiveResource> entries,
        ICollection<ArchiveValidationIssue> issues)
    {
        var duplicateVersionIds = entries
            .GroupBy(resource => resource.Metadata.VersionId)
            .Where(group => group.Count() > 1);
        foreach (var duplicate in duplicateVersionIds)
        {
            AddIssue(
                issues,
                ArchiveValidationCodes.DuplicateResourceVersion,
                ArchiveValidationSeverity.Error,
                ArchiveValidationCategory.Integrity,
                "Bundle 中存在重复的资源版本标识。",
                "/Entries",
                duplicate.First());
        }
    }

    private static void CompareNutrientAggregation(
        IEnumerable<NutrientAmount> components,
        IReadOnlyList<NutrientAmount> summary,
        string path,
        IArchiveResource resource,
        ICollection<ArchiveValidationIssue> issues)
    {
        var componentGroups = components.GroupBy(value => CodingKey(value.Nutrient)).ToDictionary(group => group.Key);
        foreach (var summaryValue in summary)
        {
            if (!componentGroups.TryGetValue(CodingKey(summaryValue.Nutrient), out var componentGroup))
            {
                if (summaryValue.Amount.Value != 0)
                {
                    AddAggregationMismatch(path, resource, issues);
                }

                continue;
            }

            if (componentGroup.Any(component => !component.Amount.Unit.HasSameIdentity(summaryValue.Amount.Unit)))
            {
                AddIssue(
                    issues,
                    ArchiveValidationCodes.IncompatibleUnits,
                    ArchiveValidationSeverity.Error,
                    ArchiveValidationCategory.Integrity,
                    "营养素组成项与汇总值单位不一致。",
                    path,
                    resource);
                continue;
            }

            if (componentGroup.Sum(component => component.Amount.Value) != summaryValue.Amount.Value)
            {
                AddAggregationMismatch(path, resource, issues);
            }
        }

        var summaryKeys = summary.Select(value => CodingKey(value.Nutrient)).ToHashSet(StringComparer.Ordinal);
        if (componentGroups.Any(group => !summaryKeys.Contains(group.Key) && group.Value.Sum(value => value.Amount.Value) != 0))
        {
            AddAggregationMismatch(path, resource, issues);
        }
    }

    private static void AddAggregationMismatch(
        string path,
        IArchiveResource resource,
        ICollection<ArchiveValidationIssue> issues) => AddIssue(
            issues,
            ArchiveValidationCodes.NutrientAggregationMismatch,
            ArchiveValidationSeverity.Error,
            ArchiveValidationCategory.Integrity,
            "营养素汇总无法由组成项逐项复核。",
            path,
            resource);

    private static void ValidateNutrientList(
        IReadOnlyList<NutrientAmount> values,
        string path,
        IArchiveResource resource,
        ICollection<ArchiveValidationIssue> issues) => ValidateDistinctCodings(
            values.Select(value => value.Nutrient),
            path,
            resource,
            issues);

    private static void ValidateReferenceData(
        ReferenceDataIdentity identity,
        ArchiveValidationScope scope,
        string path,
        IArchiveResource resource,
        ICollection<ArchiveValidationIssue> issues)
    {
        if (identity.Fingerprint is not null && identity.FingerprintAbsentReason is not null)
        {
            AddIssue(
                issues,
                ArchiveValidationCodes.ValueAndAbsentReasonConflict,
                ArchiveValidationSeverity.Error,
                ArchiveValidationCategory.Integrity,
                "参考数据内容指纹与其缺失原因不能同时存在。",
                path,
                resource);
        }
        else if (identity.Fingerprint is null && identity.FingerprintAbsentReason is null)
        {
            AddIssue(
                issues,
                ArchiveValidationCodes.ReferenceDataIdentityIncomplete,
                FormalRequirementSeverity(scope),
                ArchiveValidationCategory.Compatibility,
                "参考数据缺少内容指纹或明确缺失原因。",
                path,
                resource);
        }
    }

    private static void ValidateValueAbsentReasonChoice<TValue>(
        TValue? value,
        DataAbsentReasonCode? absentReason,
        ArchiveValidationScope scope,
        string path,
        IArchiveResource resource,
        ICollection<ArchiveValidationIssue> issues)
        where TValue : class
    {
        if (value is not null && absentReason is not null)
        {
            AddIssue(
                issues,
                ArchiveValidationCodes.ValueAndAbsentReasonConflict,
                ArchiveValidationSeverity.Error,
                ArchiveValidationCategory.Integrity,
                "值与其缺失原因不能同时存在。",
                path,
                resource);
        }
        else if (value is null && absentReason is null && resource.Metadata.Status != ResourceLifecycleStatus.Draft)
        {
            AddIssue(
                issues,
                ArchiveValidationCodes.RequiredSemanticValueMissing,
                FormalRequirementSeverity(scope),
                ArchiveValidationCategory.Integrity,
                "正式资源缺少值及其缺失原因。",
                path,
                resource);
        }
    }

    private static void ValidateDistinctAssessmentInputs(
        IEnumerable<AssessmentInput> inputs,
        string path,
        IArchiveResource resource,
        ICollection<ArchiveValidationIssue> issues)
    {
        var inputArray = inputs.ToArray();
        ValidateDistinctCodings(inputArray.Select(input => input.Parameter), path, resource, issues);
        foreach (var input in inputArray)
        {
            if (input.BasisValue is not null && input.BasisValue != input.AdoptedValue &&
                string.IsNullOrWhiteSpace(input.AdjustmentReason) && input.DerivationMethod is null)
            {
                AddIssue(
                    issues,
                    ArchiveValidationCodes.AdjustmentReasonMissing,
                    ArchiveValidationSeverity.Warning,
                    ArchiveValidationCategory.Integrity,
                    "评估输入发生调整但缺少推导方法或调整说明。",
                    path,
                    resource);
            }
        }
    }

    private static void ValidateDistinctNamedValues(
        IEnumerable<NamedArchiveValue> values,
        string path,
        IArchiveResource resource,
        ICollection<ArchiveValidationIssue> issues)
    {
        var duplicate = values
            .GroupBy(value => (Name: CodingKey(value.Name), value.Value))
            .Any(group => group.Count() > 1);
        if (duplicate)
        {
            AddIssue(
                issues,
                ArchiveValidationCodes.DuplicateLocalIdentity,
                ArchiveValidationSeverity.Warning,
                ArchiveValidationCategory.Integrity,
                "结构化输入摘要包含重复的名称编码。",
                path,
                resource);
        }
    }

    private static void ValidateDistinctCodings(
        IEnumerable<Coding> codings,
        string path,
        IArchiveResource resource,
        ICollection<ArchiveValidationIssue> issues)
    {
        if (codings.GroupBy(CodingKey, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            AddIssue(
                issues,
                ArchiveValidationCodes.DuplicateLocalIdentity,
                ArchiveValidationSeverity.Error,
                ArchiveValidationCategory.Integrity,
                "同一资源范围内包含重复编码。",
                path,
                resource);
        }
    }

    private static void ValidateDistinctVersionedReferences(
        IEnumerable<VersionedResourceReference> references,
        string path,
        IArchiveResource resource,
        ICollection<ArchiveValidationIssue> issues)
    {
        if (references.GroupBy(VersionKey, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            AddIssue(
                issues,
                ArchiveValidationCodes.DuplicateLocalIdentity,
                ArchiveValidationSeverity.Error,
                ArchiveValidationCategory.Integrity,
                "资源引用列表包含重复的确切版本。",
                path,
                resource);
        }
    }

    private static void ValidateUniqueLocalIds(
        IEnumerable<LocalIdentifier> identifiers,
        string path,
        IArchiveResource resource,
        ICollection<ArchiveValidationIssue> issues)
    {
        if (identifiers.GroupBy(identifier => identifier.Value, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            AddIssue(
                issues,
                ArchiveValidationCodes.DuplicateLocalIdentity,
                ArchiveValidationSeverity.Error,
                ArchiveValidationCategory.Integrity,
                "资源内部包含重复的局部标识。",
                path,
                resource);
        }
    }

    private static void ValidatePositiveUniqueSequences(
        IEnumerable<int> sequences,
        string path,
        IArchiveResource resource,
        ICollection<ArchiveValidationIssue> issues)
    {
        var sequenceArray = sequences.ToArray();
        if (sequenceArray.Any(sequence => sequence < 1) || sequenceArray.Distinct().Count() != sequenceArray.Length)
        {
            AddIssue(
                issues,
                ArchiveValidationCodes.InvalidTechnicalValue,
                ArchiveValidationSeverity.Error,
                ArchiveValidationCategory.Integrity,
                "显示顺序必须为不重复的正整数。",
                path,
                resource);
        }
    }

    private static void ValidateAssessmentReferenceTypes(
        LogicalResourceReference subjectReference,
        VersionedResourceReference? consultationReference,
        string prefix,
        IArchiveResource resource,
        ICollection<ArchiveValidationIssue> issues)
    {
        if (subjectReference.ExpectedResourceType is { } subjectType && subjectType != ArchiveResourceTypes.Patient)
        {
            AddIssue(
                issues,
                ArchiveValidationCodes.ReferenceTypeMismatch,
                ArchiveValidationSeverity.Error,
                ArchiveValidationCategory.Integrity,
                "评估对象引用声明的资源类型不是 Patient。",
                Path(prefix, "/SubjectReference"),
                resource);
        }

        if (consultationReference?.ExpectedResourceType is { } consultationType &&
            consultationType != ArchiveResourceTypes.Consultation)
        {
            AddIssue(
                issues,
                ArchiveValidationCodes.ReferenceTypeMismatch,
                ArchiveValidationSeverity.Error,
                ArchiveValidationCategory.Integrity,
                "所属咨询引用声明的资源类型不是 Consultation。",
                Path(prefix, "/ConsultationReference"),
                resource);
        }
    }

    private static void ValidateExtensions(
        IReadOnlyList<ArchiveExtension> extensions,
        string path,
        IArchiveResource? resource,
        ICollection<ArchiveValidationIssue> issues)
    {
        var stack = new Stack<(ArchiveExtension Extension, string Path, int Depth)>();
        for (var index = extensions.Count - 1; index >= 0; index--)
        {
            stack.Push((extensions[index], $"{path}/{index}", 1));
        }

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current.Depth > MaximumExtensionDepth)
            {
                AddIssue(
                    issues,
                    ArchiveValidationCodes.ExtensionDepthExceeded,
                    ArchiveValidationSeverity.Fatal,
                    ArchiveValidationCategory.Security,
                    "扩展嵌套超过允许的语义校验深度。",
                    current.Path,
                    resource);
                continue;
            }

            if (current.Extension.Value is not null && current.Extension.Children.Count > 0)
            {
                AddIssue(
                    issues,
                    ArchiveValidationCodes.ExtensionChoiceConflict,
                    ArchiveValidationSeverity.Error,
                    ArchiveValidationCategory.Structure,
                    "扩展不能同时包含原子值和子扩展。",
                    current.Path,
                    resource);
            }
            else if (current.Extension.Value is null && current.Extension.Children.Count == 0)
            {
                AddIssue(
                    issues,
                    ArchiveValidationCodes.EmptyExtension,
                    ArchiveValidationSeverity.Warning,
                    ArchiveValidationCategory.Structure,
                    "扩展未包含值或子扩展。",
                    current.Path,
                    resource);
            }

            for (var index = current.Extension.Children.Count - 1; index >= 0; index--)
            {
                stack.Push((
                    current.Extension.Children[index],
                    $"{current.Path}/Children/{index}",
                    current.Depth + 1));
            }
        }
    }

    private static void ValidateAssessmentReferences(
        LogicalResourceReference subject,
        VersionedResourceReference? consultation,
        string prefix,
        IArchiveResource resource,
        IReadOnlyList<IArchiveResource> entries,
        ICollection<ArchiveValidationIssue> issues)
    {
        ValidateLogicalReference(subject, prefix + "/SubjectReference", resource, entries, issues);
        ValidateOptionalVersionedReference(consultation, prefix + "/ConsultationReference", resource, entries, issues);
    }

    private static void ValidateLogicalReference(
        LogicalResourceReference reference,
        string path,
        IArchiveResource source,
        IReadOnlyList<IArchiveResource> entries,
        ICollection<ArchiveValidationIssue> issues)
    {
        var matches = entries.Where(resource => resource.Metadata.ResourceId == reference.ResourceId).ToArray();
        if (matches.Length == 0)
        {
            AddUnresolvedReference(path, source, issues);
        }
        else if (reference.ExpectedResourceType is { } expectedType && matches.All(resource => resource.ResourceType != expectedType))
        {
            AddReferenceTypeMismatch(path, source, issues);
        }
    }

    private static void ValidateOptionalVersionedReference(
        VersionedResourceReference? reference,
        string path,
        IArchiveResource source,
        IReadOnlyList<IArchiveResource> entries,
        ICollection<ArchiveValidationIssue> issues)
    {
        if (reference is not null)
        {
            ValidateVersionedReference(reference, path, source, entries, issues);
        }
    }

    private static void ValidateVersionedReference(
        VersionedResourceReference reference,
        string path,
        IArchiveResource source,
        IReadOnlyList<IArchiveResource> entries,
        ICollection<ArchiveValidationIssue> issues)
    {
        var match = entries.FirstOrDefault(resource =>
            resource.Metadata.ResourceId == reference.ResourceId &&
            resource.Metadata.VersionId == reference.VersionId);
        if (match is null)
        {
            AddUnresolvedReference(path, source, issues);
        }
        else if (reference.ExpectedResourceType is { } expectedType && match.ResourceType != expectedType)
        {
            AddReferenceTypeMismatch(path, source, issues);
        }
    }

    private static void AddUnresolvedReference(
        string path,
        IArchiveResource source,
        ICollection<ArchiveValidationIssue> issues) => AddIssue(
            issues,
            ArchiveValidationCodes.UnresolvedReference,
            ArchiveValidationSeverity.Error,
            ArchiveValidationCategory.Integrity,
            "资源引用无法在当前 Bundle 中解析。",
            path,
            source);

    private static void AddReferenceTypeMismatch(
        string path,
        IArchiveResource source,
        ICollection<ArchiveValidationIssue> issues) => AddIssue(
            issues,
            ArchiveValidationCodes.ReferenceTypeMismatch,
            ArchiveValidationSeverity.Error,
            ArchiveValidationCategory.Integrity,
            "资源引用声明的类型与实际资源类型不一致。",
            path,
            source);

    private static IArchiveResource? FindVersion(
        IEnumerable<IArchiveResource> entries,
        VersionedResourceReference reference) => entries.FirstOrDefault(resource =>
            resource.Metadata.ResourceId == reference.ResourceId &&
            resource.Metadata.VersionId == reference.VersionId);

    private static LogicalResourceReference? GetSubjectReference(IArchiveResource resource) => resource switch
    {
        EnergyAssessmentResource energy => energy.SubjectReference,
        DriAssessmentResource dri => dri.SubjectReference,
        DietaryRecallResource recall => recall.SubjectReference,
        SoapNoteResource soap => soap.SubjectReference,
        NutritionAdviceResource advice => advice.SubjectReference,
        NutritionReportResource report => report.SubjectReference,
        NutritionScaleAssessmentResource scale => scale.SubjectReference,
        _ => null
    };

    private static VersionedResourceReference? GetConsultationReference(IArchiveResource resource) => resource switch
    {
        EnergyAssessmentResource energy => energy.ConsultationReference,
        DriAssessmentResource dri => dri.ConsultationReference,
        DietaryRecallResource recall => recall.ConsultationReference,
        SoapNoteResource soap => soap.ConsultationReference,
        NutritionAdviceResource advice => advice.ConsultationReference,
        NutritionReportResource report => report.ConsultationReference,
        NutritionScaleAssessmentResource scale => scale.ConsultationReference,
        _ => null
    };

    private static bool IsKnownConsultationResource(IArchiveResource resource) => resource is
        EnergyAssessmentResource or
        DriAssessmentResource or
        DietaryRecallResource or
        SoapNoteResource or
        NutritionAdviceResource or
        NutritionReportResource or
        NutritionScaleAssessmentResource;

    private static NutrientAmount? FindNutrient(IEnumerable<NutrientAmount> values, string code) =>
        values.FirstOrDefault(value => string.Equals(value.Nutrient.Code, code, StringComparison.Ordinal));

    private static bool QuantitiesEqual(Quantity left, Quantity right) =>
        left.Value == right.Value &&
        left.Comparator == right.Comparator &&
        left.Unit.HasSameIdentity(right.Unit);

    private static bool AreDailyTargetAndMealUnitsCompatible(Coding dailyUnit, Coding mealUnit)
    {
        if (dailyUnit.HasSameIdentity(mealUnit))
        {
            return true;
        }

        return dailyUnit.System == mealUnit.System &&
            string.Equals(dailyUnit.Version, mealUnit.Version, StringComparison.Ordinal) &&
            dailyUnit.Code.EndsWith("/d", StringComparison.Ordinal) &&
            string.Equals(
                dailyUnit.Code[..^2],
                mealUnit.Code,
                StringComparison.Ordinal);
    }

    private static bool IsUcumGram(Coding unit) =>
        unit.System.AbsoluteUri == "http://unitsofmeasure.org/" &&
        string.Equals(unit.Code, "g", StringComparison.Ordinal);

    private static bool HasActorIdentity(ActorReference? actor) => actor is not null &&
        (actor.ResourceReference is not null ||
         actor.Identifier is not null ||
         !string.IsNullOrWhiteSpace(actor.Display) ||
         actor.AbsentReason is not null);

    private static void ValidateActorReference(
        ActorReference? actor,
        string path,
        IArchiveResource resource,
        ICollection<ArchiveValidationIssue> issues,
        bool allowOrganization = true)
    {
        if (actor is null)
        {
            return;
        }

        var hasIdentity = actor.ResourceReference is not null ||
            actor.Identifier is not null ||
            !string.IsNullOrWhiteSpace(actor.Display);

        if (!hasIdentity && actor.AbsentReason is null)
        {
            AddIssue(
                issues,
                ArchiveValidationCodes.RequiredSemanticValueMissing,
                ArchiveValidationSeverity.Error,
                ArchiveValidationCategory.Integrity,
                "主体引用缺少身份或明确的缺失原因。",
                path,
                resource);
        }

        if (hasIdentity && actor.AbsentReason is not null)
        {
            AddIssue(
                issues,
                ArchiveValidationCodes.ValueAndAbsentReasonConflict,
                ArchiveValidationSeverity.Error,
                ArchiveValidationCategory.Integrity,
                "主体身份与缺失原因不能同时存在。",
                path,
                resource);
        }

        if (actor.Organization is null)
        {
            return;
        }

        if (!allowOrganization)
        {
            AddIssue(
                issues,
                ArchiveValidationCodes.InvalidTechnicalValue,
                ArchiveValidationSeverity.Error,
                ArchiveValidationCategory.Integrity,
                "行为时机构快照不能继续嵌套所属机构。",
                path + "/Organization",
                resource);
            return;
        }

        ValidateActorReference(
            actor.Organization,
            path + "/Organization",
            resource,
            issues,
            allowOrganization: false);
    }

    private static bool HasEnteredInErrorFields(ResourceMetadata metadata) =>
        metadata.EnteredInErrorReason is not null ||
        !string.IsNullOrWhiteSpace(metadata.EnteredInErrorReasonText) ||
        metadata.EnteredInErrorAt is not null ||
        metadata.EnteredInErrorBy is not null;

    private static bool IsSelfReference(
        VersionedResourceReference? reference,
        ResourceMetadata metadata) => reference is not null &&
        reference.ResourceId == metadata.ResourceId &&
        reference.VersionId == metadata.VersionId;

    private static ArchiveValidationSeverity FormalRequirementSeverity(ArchiveValidationScope scope) =>
        scope == ArchiveValidationScope.Import
            ? ArchiveValidationSeverity.Warning
            : ArchiveValidationSeverity.Error;

    private static string CodingKey(Coding coding) =>
        $"{coding.System.AbsoluteUri}|{coding.Code}|{coding.Version}";

    private static string VersionKey(VersionedResourceReference reference) =>
        VersionKey(reference.ResourceId, reference.VersionId);

    private static string VersionKey(ResourceId resourceId, ResourceVersionId versionId) =>
        $"{resourceId.Value:D}|{versionId.Value:D}";

    private static string Path(string prefix, string suffix) => prefix + suffix;

    private static void AddIssue(
        ICollection<ArchiveValidationIssue> issues,
        string code,
        ArchiveValidationSeverity severity,
        ArchiveValidationCategory category,
        string message,
        string path,
        IArchiveResource? resource)
    {
        issues.Add(new ArchiveValidationIssue
        {
            Code = code,
            Severity = severity,
            Category = category,
            Message = message,
            Path = new ArchiveElementPath(path),
            ResourceReference = resource is null
                ? null
                : new VersionedResourceReference(
                    resource.Metadata.ResourceId,
                    resource.Metadata.VersionId,
                    resource.ResourceType)
        });
    }
}
