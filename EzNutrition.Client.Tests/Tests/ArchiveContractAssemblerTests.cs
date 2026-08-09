using EzNutrition.Archives.Contracts.Abstractions;
using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Archives.Contracts.Serialization;
using EzNutrition.Archives.Contracts.Validation;
using EzNutrition.Archives.Contracts.ValueObjects;
using EzNutrition.Application.Archives;
using EzNutrition.Application.Consultations;
using EzNutrition.Domain.Consultations;
using EzNutrition.Client.Tests.Fixtures;

namespace EzNutrition.Client.Tests.Tests;

/// <summary>
/// 验证 WASM 运行态咨询到档案契约的显式映射。
/// </summary>
public sealed class ArchiveContractAssemblerTests
{
    private static readonly ArchiveContractAssembler Assembler = new(new ApplicationIdentity(
        new Uri("https://eznutrition.cdorey.net/applications/test-wasm-client"),
        "EzNutrition WASM 合成测试",
        "1.0-test"));
    private static readonly ArchiveContractValidator Validator = new();

    /// <summary>
    /// 验证八类运行态样本均可形成引用闭合的咨询文档。
    /// </summary>
    [Fact]
    public async Task All_runtime_samples_map_to_reference_closed_documents()
    {
        var samples = await RuntimeArchiveSamples.CreateAllAsync();

        Assert.Equal(8, samples.Count);
        Assert.Equal(samples.Count, samples.Select(sample => sample.Key).Distinct(StringComparer.Ordinal).Count());
        foreach (var sample in samples)
        {
            var document = CreateDocument(sample.Archive);
            AssertDocumentClosure(document);
            var validation = Validator.ValidateBundle(document.Bundle, ArchiveValidationScope.DraftSave);
            Assert.False(
                validation.HasErrors,
                $"{sample.Key}: {string.Join(" | ", validation.Issues.Select(issue => issue.Code))}");

            var patient = Assert.Single(document.Bundle.Entries.OfType<PatientResource>());
            Assert.Equal(sample.Archive.ContractIdentity.Patient.ResourceId, patient.Metadata.ResourceId);
            Assert.Equal(sample.Archive.ContractIdentity.Patient.VersionId, patient.Metadata.VersionId);
            Assert.DoesNotContain(
                document.Bundle.Entries,
                resource => resource.Metadata.ResourceId.Value == ((ClientInfo)sample.Archive.Client).ClientId);
        }
    }

    /// <summary>
    /// 验证同一浏览器咨询的多次快照保持资源身份稳定并产生不同 Bundle 身份。
    /// </summary>
    [Fact]
    public void Repeated_snapshots_keep_resource_and_entry_identity_stable()
    {
        var sample = RuntimeArchiveSamples.CreateAdultAutomaticEnergy().Archive;
        var first = Assembler.CreateDocument(sample, sample.ContractIdentity.CreatedAt.AddMinutes(30));
        var second = Assembler.CreateDocument(sample, sample.ContractIdentity.CreatedAt.AddMinutes(45));

        Assert.NotEqual(first.Bundle.BundleId, second.Bundle.BundleId);
        Assert.Equal(
            ResourceKeys(first),
            ResourceKeys(second));
        Assert.All(first.Bundle.Entries, resource => Assert.Equal(
            sample.ContractIdentity.CreatedAt,
            resource.Metadata.CreatedAt));
        Assert.All(second.Bundle.Entries, resource => Assert.Equal(
            sample.ContractIdentity.CreatedAt.AddMinutes(45),
            resource.Metadata.LastModifiedAt));
    }

    /// <summary>
    /// 验证自动结果、专业修正值和基于采用值形成的分配方案分别保存。
    /// </summary>
    [Fact]
    public void Energy_mapping_distinguishes_automatic_result_from_professional_adjustment()
    {
        var automaticArchive = RuntimeArchiveSamples.CreateAdultAutomaticEnergy().Archive;
        var automatic = SingleResource<EnergyAssessmentResource>(CreateDocument(automaticArchive));
        var automaticCandidate = Assert.Single(automatic.CandidateCalculations);
        var automaticDecision = Assert.IsType<ProfessionalEnergyDecision>(automatic.ProfessionalDecision);

        Assert.Equal(
            (decimal)automaticArchive.CurrentEnergyCalculator!.CalculatedEnergy!.Value,
            automaticCandidate.Result.Value);
        Assert.Equal(automaticCandidate.Result.Value, automaticDecision.AdoptedEnergyTarget.Value);
        Assert.Equal("automatic-calculation", automaticDecision.DecisionBasis?.Code);

        var adjustedArchive = RuntimeArchiveSamples.CreateAdultManualEnergy().Archive;
        var adjusted = SingleResource<EnergyAssessmentResource>(CreateDocument(adjustedArchive));
        var adjustedCandidate = Assert.Single(adjusted.CandidateCalculations);
        var adjustedDecision = Assert.IsType<ProfessionalEnergyDecision>(adjusted.ProfessionalDecision);
        var allocation = Assert.IsType<EnergyAllocationPlan>(adjusted.AllocationPlan);

        Assert.Equal(
            (decimal)adjustedArchive.CurrentEnergyCalculator!.CalculatedEnergy!.Value,
            adjustedCandidate.Result.Value);
        Assert.Equal(2100, adjustedDecision.AdoptedEnergyTarget.Value);
        Assert.NotEqual(adjustedCandidate.Result.Value, adjustedDecision.AdoptedEnergyTarget.Value);
        Assert.Equal("professional-adjustment", adjustedDecision.DecisionBasis?.Code);
        Assert.Equal(adjustedDecision.AdoptedEnergyTarget, allocation.EnergyTarget);
        Assert.Equal(1m, allocation.MacronutrientTargets.Sum(target => target.EnergyFraction));
        Assert.All(allocation.MacronutrientTargets, target => Assert.Equal(
            target.DailyAmount.Value,
            target.MealAllocations.Sum(meal => meal.Amount.Value)));
    }

    /// <summary>
    /// 验证 DRIs 基础值、偏移分量及无法自动解决的冲突均原样进入契约。
    /// </summary>
    [Fact]
    public void Dri_mapping_preserves_components_and_marks_unresolved_values()
    {
        var pregnancy = SingleResource<DriAssessmentResource>(
            CreateDocument(RuntimeArchiveSamples.CreatePregnancyDri().Archive));
        var calcium = pregnancy.NutrientResults.Single(result => result.Nutrient.Code == "calcium");
        var calciumRni = calcium.ReferenceValues.Single(value => value.ReferenceType.Code == "RNI");
        var choline = pregnancy.NutrientResults.Single(result => result.Nutrient.Code == "choline");
        var adoptedCalcium = Assert.IsType<QuantityArchiveValue>(calciumRni.AdoptedValue);

        Assert.Equal(1000, adoptedCalcium.Value.Value);
        Assert.Equal(new[] { 800m, 200m }, calciumRni.Components.Select(component => component.Value.Value));
        Assert.Single(calciumRni.Components, component => component.IsOffset);
        Assert.Null(calciumRni.AbsentReason);
        Assert.Equal("AI", Assert.Single(choline.ReferenceValues).ReferenceType.Code);

        var conflictArchive = RuntimeArchiveSamples.CreateDriConflict().Archive;
        var conflict = SingleResource<DriAssessmentResource>(CreateDocument(conflictArchive));
        var zinc = Assert.Single(conflict.NutrientResults);
        var zincRni = Assert.Single(zinc.ReferenceValues);

        Assert.Single(conflictArchive.DRIs!.AggregationIssues);
        Assert.Equal(2, zincRni.Components.Count);
        Assert.Null(zincRni.BasisValue);
        Assert.Null(zincRni.AdoptedValue);
        Assert.Equal(DataAbsentReasonCode.NotEstablished, zincRni.AbsentReason);
    }

    /// <summary>
    /// 验证食物贡献、餐次汇总、全日汇总和宏量营养素能量能够逐层复核。
    /// </summary>
    [Fact]
    public async Task Dietary_mapping_reconciles_entries_meals_totals_and_energy()
    {
        var sample = await RuntimeArchiveSamples.CreateOlderAdultDietaryRecallAsync();
        var recall = SingleResource<DietaryRecallResource>(CreateDocument(sample.Archive));

        Assert.Equal(DietaryRecallStatus.IntakeReported, recall.Status);
        Assert.Equal(3, recall.Meals.Count);
        Assert.NotNull(recall.GuidanceSnapshot);
        Assert.Equal(DataAbsentReasonCode.NotAsked, recall.RecallPeriodAbsentReason);
        Assert.Equal(0.75m, recall.Meals.Single(meal => meal.Occasion.Code == "lunch")
            .Entries.Single().EdibleFraction);

        foreach (var total in recall.TotalNutrientSummary.Where(IsMacronutrientOrEnergy))
        {
            var mealSum = recall.Meals
                .SelectMany(meal => meal.NutrientSummary)
                .Where(value => value.Nutrient.HasSameIdentity(total.Nutrient))
                .Sum(value => value.Amount.Value);
            Assert.Equal(total.Amount.Value, mealSum);
        }

        foreach (var meal in recall.Meals)
        {
            foreach (var summary in meal.NutrientSummary)
            {
                var entrySum = meal.Entries
                    .SelectMany(entry => entry.NutrientContributions)
                    .Where(value => value.Nutrient.HasSameIdentity(summary.Nutrient))
                    .Sum(value => value.Amount.Value);
                Assert.Equal(summary.Amount.Value, entrySum);
            }
        }

        var consistency = Assert.IsType<DietaryEnergyConsistency>(recall.EnergyConsistency);
        Assert.Equal(690, consistency.RecordedTotalEnergy.Value);
        Assert.Equal(690, consistency.MacronutrientDerivedEnergy.Value);
        Assert.Null(consistency.AllowedDifference);
        Assert.Equal(DataAbsentReasonCode.NotEstablished, consistency.AllowedDifferenceAbsentReason);
    }

    /// <summary>
    /// 验证空白膳食草稿可以安全核算为零值快照，不发生除零异常。
    /// </summary>
    [Fact]
    public async Task Empty_dietary_draft_calculates_and_maps_without_division_by_zero()
    {
        var sample = await RuntimeArchiveSamples.CreateEmptyDietaryDraftAsync();
        var document = CreateDocument(sample.Archive);
        var recall = SingleResource<DietaryRecallResource>(document);
        var validation = Validator.ValidateBundle(document.Bundle, ArchiveValidationScope.DraftSave);

        Assert.Null(recall.Status);
        Assert.Empty(recall.Meals);
        Assert.All(recall.TotalNutrientSummary, value => Assert.Equal(0m, value.Amount.Value));
        Assert.False(validation.HasErrors);
    }

    /// <summary>
    /// 验证 SOAP、AI 上下文和需要专业判断的生理状态组合不会被映射层删除。
    /// </summary>
    [Fact]
    public void Soap_advice_and_unusual_physiology_are_preserved()
    {
        var archive = RuntimeArchiveSamples.CreateUnusualPhysiologyWithAdvice().Archive;
        var document = CreateDocument(archive);
        var consultation = SingleResource<ConsultationResource>(document);
        var soap = SingleResource<SoapNoteResource>(document);
        var advice = SingleResource<NutritionAdviceResource>(document);

        Assert.Equal("male", consultation.SubjectSnapshot?.AdministrativeSex?.Code);
        Assert.Contains(
            consultation.SubjectSnapshot?.PhysiologicalStates ?? [],
            state => state.Code == "pregnancy-third-trimester");
        Assert.Equal("由专业人员判断该组合。", soap.Assessment);
        Assert.Equal(NutritionAdviceGenerationStatus.Completed, advice.GenerationStatus);
        Assert.Equal("合成营养建议。", advice.NarrativeContent);
        Assert.Equal("合成推理摘要。", advice.ReasoningContent);
        Assert.Equal("合成模型服务", advice.Generator?.Method.Display);
        Assert.Contains(advice.InputSummary, input => input.Name.Code == "height");
        Assert.Contains(advice.InputSummary, input => input.Name.Code == "weight");
        Assert.Contains(advice.InputSummary, input => input.Name.Code == "deficient-nutrient");
        Assert.DoesNotContain(advice.InputResourceReferences, reference =>
            reference.ExpectedResourceType == ArchiveResourceTypes.NutritionAdvice);
    }

    private static ArchiveDocument CreateDocument(ConsultationWorkspace archive) =>
        Assembler.CreateDocument(archive, archive.ContractIdentity.CreatedAt.AddHours(1));

    private static TResource SingleResource<TResource>(ArchiveDocument document)
        where TResource : class, IArchiveResource =>
        Assert.Single(document.Bundle.Entries.OfType<TResource>());

    private static IReadOnlyList<string> ResourceKeys(ArchiveDocument document) => document.Bundle.Entries
        .Select(resource => $"{resource.ResourceType.Value}/{resource.Metadata.ResourceId}/{resource.Metadata.VersionId}")
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToArray();

    private static bool IsMacronutrientOrEnergy(NutrientAmount value) =>
        value.Nutrient.Code is "energy" or "protein" or "total-fat" or "carbohydrate";

    private static void AssertDocumentClosure(ArchiveDocument document)
    {
        var entries = document.Bundle.Entries;
        Assert.NotEmpty(entries);
        Assert.Equal(
            entries.Count,
            entries.Select(resource => resource.Metadata.VersionId).Distinct().Count());
        var patient = Assert.Single(entries.OfType<PatientResource>());
        var consultation = Assert.Single(entries.OfType<ConsultationResource>());

        AssertLogicalReference(consultation.SubjectReference, patient);
        Assert.Equal(entries.Count - 2, consultation.ClinicalResourceReferences.Count);
        Assert.All(consultation.ClinicalResourceReferences, reference => AssertVersionedReference(reference, entries));

        foreach (var resource in entries)
        {
            switch (resource)
            {
                case EnergyAssessmentResource energy:
                    AssertAssessmentReferences(energy.SubjectReference, energy.ConsultationReference, patient, consultation);
                    break;
                case DriAssessmentResource dri:
                    AssertAssessmentReferences(dri.SubjectReference, dri.ConsultationReference, patient, consultation);
                    break;
                case DietaryRecallResource recall:
                    AssertAssessmentReferences(recall.SubjectReference, recall.ConsultationReference, patient, consultation);
                    break;
                case SoapNoteResource soap:
                    AssertAssessmentReferences(soap.SubjectReference, soap.ConsultationReference, patient, consultation);
                    break;
                case NutritionAdviceResource advice:
                    AssertAssessmentReferences(advice.SubjectReference, advice.ConsultationReference, patient, consultation);
                    Assert.All(advice.InputResourceReferences, reference => AssertVersionedReference(reference, entries));
                    break;
            }
        }
    }

    private static void AssertAssessmentReferences(
        LogicalResourceReference subjectReference,
        VersionedResourceReference? consultationReference,
        PatientResource patient,
        ConsultationResource consultation)
    {
        AssertLogicalReference(subjectReference, patient);
        Assert.NotNull(consultationReference);
        Assert.Equal(consultation.Metadata.ResourceId, consultationReference.ResourceId);
        Assert.Equal(consultation.Metadata.VersionId, consultationReference.VersionId);
        Assert.Equal(ArchiveResourceTypes.Consultation, consultationReference.ExpectedResourceType);
    }

    private static void AssertLogicalReference(
        LogicalResourceReference reference,
        IArchiveResource expectedResource)
    {
        Assert.Equal(expectedResource.Metadata.ResourceId, reference.ResourceId);
        Assert.Equal(expectedResource.ResourceType, reference.ExpectedResourceType);
    }

    private static void AssertVersionedReference(
        VersionedResourceReference reference,
        IReadOnlyList<IArchiveResource> entries)
    {
        Assert.Contains(entries, resource =>
            resource.Metadata.ResourceId == reference.ResourceId &&
            resource.Metadata.VersionId == reference.VersionId &&
            (reference.ExpectedResourceType is null || resource.ResourceType == reference.ExpectedResourceType));
    }
}
