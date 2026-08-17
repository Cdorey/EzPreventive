using EzNutrition.Archives.Contracts.Abstractions;
using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Archives.Contracts.Serialization;
using EzNutrition.Archives.Contracts.Validation;
using EzNutrition.Archives.Contracts.ValueObjects;
using EzNutrition.Application.Archives;
using EzNutrition.Application.Consultations;
using EzNutrition.Domain.Assessments;
using EzNutrition.Domain.Consultations;
using EzNutrition.Domain.Dietary;
using EzNutrition.Client.Tests.Fixtures;
using EzNutrition.Shared.Data.Entities;

namespace EzNutrition.Client.Tests.Tests;

/// <summary>
/// 验证五十例合成咨询能够由当前 WASM 计算层处理并完整映射到档案契约。
/// </summary>
public sealed class ConsultationScenarioTests
{
    private static readonly ArchiveContractAssembler Assembler = new(new ApplicationIdentity(
        new Uri("https://eznutrition.cdorey.net/applications/scenario-test-client"),
        "EzNutrition 场景测试",
        "1.0-test"));
    private static readonly ArchiveContractValidator Validator = new();

    /// <summary>
    /// 获取可供 xUnit 独立执行的五十个场景键。
    /// </summary>
    public static IEnumerable<object[]> ScenarioKeys() =>
        ConsultationScenarioCatalog.Keys.Select(key => new object[] { key });

    /// <summary>
    /// 验证场景目录数量、命名和覆盖阶段保持稳定。
    /// </summary>
    [Fact]
    public void Catalog_contains_fifty_unique_scenarios_across_all_workflow_stages()
    {
        var keys = ConsultationScenarioCatalog.Keys;

        Assert.Equal(50, keys.Count);
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
        foreach (var workflow in Enum.GetValues<ConsultationWorkflow>())
        {
            Assert.Equal(10, keys.Count(key => key.EndsWith(WorkflowCode(workflow), StringComparison.Ordinal)));
        }
    }

    /// <summary>
    /// 验证每例咨询的运行态数据、计算结果、引用关系和必要原始事实均可进入 Contracts。
    /// </summary>
    [Theory]
    [MemberData(nameof(ScenarioKeys))]
    public async Task Scenario_completes_runtime_processing_and_preserves_required_contract_facts(string key)
    {
        var scenario = await ConsultationScenarioCatalog.CreateAsync(key);
        var archive = scenario.Archive;
        var document = Assembler.CreateDocument(
            archive,
            archive.ContractIdentity.CreatedAt.AddHours(2),
            new ArchiveBundleId(StableBundleId(key)));
        var validation = Validator.ValidateBundle(document.Bundle, ArchiveValidationScope.DraftSave);

        Assert.False(
            validation.HasErrors,
            $"{key}: {string.Join(" | ", validation.Issues.Select(issue => $"{issue.Code}@{issue.Path}"))}");
        AssertResourcePresence(scenario, document);
        AssertReferenceClosure(document);
        AssertPatientAndSnapshot(archive, document);
        AssertEnergy(archive, document);
        AssertDris(scenario, document);
        AssertDietaryRecall(scenario, document);
        AssertSoap(archive, document);
        AssertAdvice(scenario, document);
    }

    private static void AssertResourcePresence(ConsultationScenario scenario, ArchiveDocument document)
    {
        var archive = scenario.Archive;
        Assert.Single(document.Bundle.Entries.OfType<PatientResource>());
        Assert.Single(document.Bundle.Entries.OfType<ConsultationResource>());
        Assert.Equal(archive.CurrentEnergyCalculator is null ? 0 : 1, document.Bundle.Entries.OfType<EnergyAssessmentResource>().Count());
        Assert.Equal(archive.DRIs is null ? 0 : 1, document.Bundle.Entries.OfType<DriAssessmentResource>().Count());
        Assert.Equal(archive.DietaryRecallSurvey is null ? 0 : 1, document.Bundle.Entries.OfType<DietaryRecallResource>().Count());
        Assert.Equal(archive.SubjectiveObjectiveAssessmentPlanInformation is null ? 0 : 1, document.Bundle.Entries.OfType<SoapNoteResource>().Count());
        Assert.Equal(scenario.ExpectedAdviceStatus is null ? 0 : 1, document.Bundle.Entries.OfType<NutritionAdviceResource>().Count());
    }

    private static void AssertPatientAndSnapshot(
        ConsultationWorkspace archive,
        ArchiveDocument document)
    {
        var client = Assert.IsType<ClientInfo>(archive.Client);
        var patient = Assert.Single(document.Bundle.Entries.OfType<PatientResource>());
        var consultation = Assert.Single(document.Bundle.Entries.OfType<ConsultationResource>());
        var snapshot = Assert.IsType<SubjectSnapshot>(consultation.SubjectSnapshot);

        Assert.Equal(archive.ContractIdentity.Patient.ResourceId, patient.Metadata.ResourceId);
        Assert.Equal(archive.ContractIdentity.Patient.VersionId, patient.Metadata.VersionId);
        Assert.NotEqual(client.ClientId, patient.Metadata.ResourceId.Value);
        var age = Assert.IsType<EzNutrition.Domain.Consultations.ChronologicalAge>(client.Age);
        var structuredAge = Assert.IsType<EzNutrition.Archives.Contracts.ValueObjects.ChronologicalAge>(
            snapshot.ChronologicalAgeAtConsultation);
        Assert.Equal(age.Years, structuredAge.Years);
        Assert.Equal(age.Months, structuredAge.Months);
        Assert.Equal(age.Days, structuredAge.Days);
        Assert.Equal(age.Years, Assert.IsType<Quantity>(snapshot.AgeAtConsultation).Value);
        Assert.Equal(Normalize(client.Name), snapshot.IdentityDisplay);
        Assert.Equal(Normalize(client.Name), patient.Names.SingleOrDefault()?.Text);
        Assert.Equal(
            string.IsNullOrWhiteSpace(client.Name) ? PatientIdentityMode.Unlinked : PatientIdentityMode.Identified,
            patient.IdentityMode);
        Assert.Equal(client.Gender, snapshot.AdministrativeSex?.Display);
        AssertMeasurement(client.Height, snapshot.Height);
        AssertMeasurement(client.Weight, snapshot.Weight);

        if (string.IsNullOrWhiteSpace(client.SpecialPhysiologicalPeriod))
        {
            Assert.Empty(snapshot.PhysiologicalStates);
        }
        else
        {
            Assert.Contains(snapshot.PhysiologicalStates, state =>
                string.Equals(state.Display, client.SpecialPhysiologicalPeriod, StringComparison.Ordinal));
        }
    }

    private static void AssertEnergy(
        ConsultationWorkspace archive,
        ArchiveDocument document)
    {
        if (archive.CurrentEnergyCalculator is not { } calculator)
        {
            Assert.Empty(document.Bundle.Entries.OfType<EnergyAssessmentResource>());
            return;
        }

        var energy = Assert.Single(document.Bundle.Entries.OfType<EnergyAssessmentResource>());
        if (calculator.CalculatedEnergy is null)
        {
            Assert.Empty(energy.CandidateCalculations);
            Assert.Null(energy.ProfessionalDecision);
            Assert.Equal(DataAbsentReasonCode.NotEstablished, energy.ProfessionalDecisionAbsentReason);
            Assert.Null(energy.AllocationPlan);
            return;
        }

        var candidate = Assert.Single(energy.CandidateCalculations);
        var decision = Assert.IsType<ProfessionalEnergyDecision>(energy.ProfessionalDecision);
        var allocation = Assert.IsType<EnergyAllocationPlan>(energy.AllocationPlan);
        Assert.Equal((decimal)calculator.CalculatedEnergy.Value, candidate.Result.Value);
        Assert.Equal((decimal)calculator.Energy!.Value, decision.AdoptedEnergyTarget.Value);
        Assert.Equal(decision.AdoptedEnergyTarget, allocation.EnergyTarget);
        Assert.Equal(1m, allocation.MacronutrientTargets.Sum(target => target.EnergyFraction));
        Assert.All(allocation.MacronutrientTargets, target => Assert.Equal(
            target.DailyAmount.Value,
            target.MealAllocations.Sum(meal => meal.Amount.Value)));

        if (calculator.IsEnergyManuallyAdjusted)
        {
            Assert.Equal("professional-adjustment", decision.DecisionBasis?.Code);
            Assert.NotEqual(candidate.Result.Value, decision.AdoptedEnergyTarget.Value);
            Assert.False(string.IsNullOrWhiteSpace(decision.Reason));
        }
        else
        {
            Assert.Equal("automatic-calculation", decision.DecisionBasis?.Code);
            Assert.Equal(candidate.Result.Value, decision.AdoptedEnergyTarget.Value);
        }

        var expectedMethod = calculator.CalculationMethod switch
        {
            EnergyCalculationMethod.IdealBodyWeightBeePal => "ideal-body-weight-bee-pal",
            EnergyCalculationMethod.PopulationAverage => "population-average-eer",
            _ => throw new InvalidOperationException("已计算能量缺少计算路径。")
        };
        Assert.Equal(expectedMethod, candidate.Algorithm.Method.Code);
        Assert.Equal(
            calculator.AppliedOffsetEnergy > 0,
            candidate.IntermediateResults.Any(result => result.Name.Code == "physiological-energy-offset"));
    }

    private static void AssertDris(ConsultationScenario scenario, ArchiveDocument document)
    {
        var runtimeDris = scenario.Archive.DRIs;
        if (runtimeDris is null)
        {
            Assert.Empty(document.Bundle.Entries.OfType<DriAssessmentResource>());
            return;
        }

        var resource = Assert.Single(document.Bundle.Entries.OfType<DriAssessmentResource>());
        Assert.Equal(
            runtimeDris.AvailableDRIs.Count,
            resource.NutrientResults.SelectMany(result => result.ReferenceValues).Sum(value => value.Components.Count));
        Assert.Equal(scenario.ExpectsDriConflict, runtimeDris.AggregationIssues.Count > 0);

        foreach (var runtimeGroup in runtimeDris.AvailableDRIs.GroupBy(record => new
        {
            Nutrient = record.Nutrient?.Trim(),
            record.RecordType
        }))
        {
            var nutrient = resource.NutrientResults.Single(result =>
                string.Equals(result.Nutrient.Display, runtimeGroup.Key.Nutrient, StringComparison.Ordinal));
            var value = nutrient.ReferenceValues.Single(reference =>
                string.Equals(reference.ReferenceType.Code, runtimeGroup.Key.RecordType.ToString(), StringComparison.Ordinal));
            Assert.Equal(
                runtimeGroup.Select(ComponentKey).OrderBy(item => item, StringComparer.Ordinal),
                value.Components.Select(ComponentKey).OrderBy(item => item, StringComparer.Ordinal));
        }

        if (scenario.ExpectsDriConflict)
        {
            var unresolved = resource.NutrientResults
                .SelectMany(result => result.ReferenceValues)
                .Single(value => value.Components.Count > 1);
            Assert.Null(unresolved.BasisValue);
            Assert.Null(unresolved.AdoptedValue);
            Assert.Equal(DataAbsentReasonCode.NotEstablished, unresolved.AbsentReason);
        }
    }

    private static void AssertDietaryRecall(ConsultationScenario scenario, ArchiveDocument document)
    {
        var survey = scenario.Archive.DietaryRecallSurvey;
        if (survey is null)
        {
            Assert.Empty(document.Bundle.Entries.OfType<DietaryRecallResource>());
            return;
        }

        var recall = Assert.Single(document.Bundle.Entries.OfType<DietaryRecallResource>());
        var mappedEntries = recall.Meals.SelectMany(meal => meal.Entries).ToArray();
        Assert.Equal(scenario.ExpectedDietaryEntryCount, survey.RecallEntries.Count);
        Assert.Equal(survey.RecallEntries.Count, mappedEntries.Length);
        Assert.Equal(
            survey.RecallEntries.Select(entry => $"entry-{entry.EntryId:D}").OrderBy(value => value, StringComparer.Ordinal),
            mappedEntries.Select(entry => entry.EntryId.Value).OrderBy(value => value, StringComparer.Ordinal));

        foreach (var source in survey.RecallEntries)
        {
            var mapped = mappedEntries.Single(entry => entry.EntryId.Value == $"entry-{source.EntryId:D}");
            var edibleFraction = source.IsAllEdible ? 1m : (source.Food.EdiblePortion ?? 100) / 100m;
            var consumedAmount = source.Weight * edibleFraction;
            Assert.Equal(source.Weight, mapped.ReportedAmount.Value);
            Assert.Equal(edibleFraction, mapped.EdibleFraction);
            Assert.Equal(consumedAmount, mapped.AdoptedConsumedAmount?.Value);
            Assert.Equal(source.Food.FoodNutrientValues?.Count ?? 0, mapped.NutrientContributions.Count);
            foreach (var sourceValue in source.Food.FoodNutrientValues ?? [])
            {
                var mappedValue = mapped.NutrientContributions.Single(value =>
                    string.Equals(value.Nutrient.Display, sourceValue.Nutrient?.FriendlyName, StringComparison.Ordinal));
                Assert.Equal(sourceValue.Value * consumedAmount / 100m, mappedValue.Amount.Value);
            }
        }

        AssertMealAndDayAggregation(recall);
        if (survey.SummaryCalculationTable is null)
        {
            Assert.Empty(recall.TotalNutrientSummary);
            Assert.Null(recall.EnergyConsistency);
        }
        else
        {
            Assert.Equal(survey.Nutrients.Count(), recall.TotalNutrientSummary.Count);
            foreach (var nutrient in survey.Nutrients)
            {
                var mapped = recall.TotalNutrientSummary.Single(value =>
                    string.Equals(value.Nutrient.Display, nutrient.FriendlyName, StringComparison.Ordinal));
                Assert.Equal(survey.SummaryCalculationTable[nutrient], mapped.Amount.Value);
            }

            var consistency = Assert.IsType<DietaryEnergyConsistency>(recall.EnergyConsistency);
            var independentMacroEnergy = survey.SummaryCalculationTable["蛋白质"] * 4m
                + survey.SummaryCalculationTable["脂肪"] * 9m
                + survey.SummaryCalculationTable["碳水化合物"] * 4m;
            Assert.Equal(survey.SummaryCalculationTable.TotalEnergy, consistency.RecordedTotalEnergy.Value);
            Assert.Equal(independentMacroEnergy, consistency.MacronutrientDerivedEnergy.Value);
        }
    }

    private static void AssertMealAndDayAggregation(DietaryRecallResource recall)
    {
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

        foreach (var total in recall.TotalNutrientSummary)
        {
            var mealSum = recall.Meals
                .SelectMany(meal => meal.NutrientSummary)
                .Where(value => value.Nutrient.HasSameIdentity(total.Nutrient))
                .Sum(value => value.Amount.Value);
            Assert.Equal(total.Amount.Value, mealSum);
        }
    }

    private static void AssertSoap(
        ConsultationWorkspace archive,
        ArchiveDocument document)
    {
        if (archive.SubjectiveObjectiveAssessmentPlanInformation is not { } source)
        {
            Assert.Empty(document.Bundle.Entries.OfType<SoapNoteResource>());
            return;
        }

        var soap = Assert.Single(document.Bundle.Entries.OfType<SoapNoteResource>());
        Assert.Equal(Normalize(source.Subjective), soap.Subjective);
        Assert.Equal(Normalize(source.Objective), soap.Objective);
        Assert.Equal(Normalize(source.Assessment), soap.Assessment);
        Assert.Equal(Normalize(source.Plan), soap.Plan);
    }

    private static void AssertAdvice(ConsultationScenario scenario, ArchiveDocument document)
    {
        if (scenario.ExpectedAdviceStatus is not { } expectedStatus)
        {
            Assert.Empty(document.Bundle.Entries.OfType<NutritionAdviceResource>());
            return;
        }

        var source = Assert.IsType<AiGeneratedAdvice>(scenario.Archive.AiGeneratedAdvice);
        var advice = Assert.Single(document.Bundle.Entries.OfType<NutritionAdviceResource>());
        Assert.Equal(expectedStatus, advice.GenerationStatus);
        Assert.Equal(source.RequestedAt, advice.RequestedAt);
        Assert.Equal(source.CompletedAt, advice.CompletedAt);
        Assert.Equal(Normalize(source.ReasoningContent), advice.ReasoningContent);
        Assert.Equal(Normalize(source.Content), advice.NarrativeContent);
        Assert.NotEmpty(advice.InputResourceReferences);

        var prompt = Assert.IsType<EzNutrition.Shared.Data.DTO.PromptDto.AiAdviceRequestDto>(
            scenario.Archive.AdvicePrompt);
        AssertNamedQuantity(advice, "age", prompt.PatientInfo.Age);
        AssertNamedQuantity(advice, "height", prompt.PatientInfo.Height);
        AssertNamedQuantity(advice, "weight", prompt.PatientInfo.Weight);
        AssertNamedQuantity(advice, "adopted-energy", prompt.PatientInfo.TotalBalanceEnergyViaCalculation);
    }

    private static void AssertNamedQuantity(
        NutritionAdviceResource advice,
        string code,
        decimal? expected)
    {
        var values = advice.InputSummary.Where(value => value.Name.Code == code).ToArray();
        if (expected is null)
        {
            Assert.Empty(values);
            return;
        }

        var value = Assert.IsType<QuantityArchiveValue>(Assert.Single(values).Value);
        Assert.Equal(expected.Value, value.Value.Value);
    }

    private static void AssertReferenceClosure(ArchiveDocument document)
    {
        var entries = document.Bundle.Entries;
        var patient = Assert.Single(entries.OfType<PatientResource>());
        var consultation = Assert.Single(entries.OfType<ConsultationResource>());
        Assert.Equal(entries.Count - 2, consultation.ClinicalResourceReferences.Count);
        Assert.Equal(patient.Metadata.ResourceId, consultation.SubjectReference.ResourceId);

        foreach (var reference in consultation.ClinicalResourceReferences)
        {
            Assert.Contains(entries, resource => Matches(reference, resource));
        }

        foreach (var resource in entries)
        {
            var subject = resource switch
            {
                EnergyAssessmentResource value => value.SubjectReference,
                DriAssessmentResource value => value.SubjectReference,
                DietaryRecallResource value => value.SubjectReference,
                SoapNoteResource value => value.SubjectReference,
                NutritionAdviceResource value => value.SubjectReference,
                _ => null
            };
            if (subject is not null)
            {
                Assert.Equal(patient.Metadata.ResourceId, subject.ResourceId);
            }
        }
    }

    private static bool Matches(VersionedResourceReference reference, IArchiveResource resource) =>
        reference.ResourceId == resource.Metadata.ResourceId
        && reference.VersionId == resource.Metadata.VersionId
        && (reference.ExpectedResourceType is null || reference.ExpectedResourceType == resource.ResourceType);

    private static string ComponentKey(DietaryReferenceIntakeValue value) =>
        $"{value.Value}|{value.MeasureUnit}|{value.IsOffset}|{value.AgeStart}|{value.Gender}|{value.SpecialPhysiologicalPeriod}";

    private static string ComponentKey(DriReferenceComponent value) =>
        $"{value.Value.Value}|{value.Value.Unit.Display}|{value.IsOffset}|{value.MinimumAge?.Value}|{value.PopulationSex?.Display}|{value.PhysiologicalState?.Display}";

    private static void AssertMeasurement(decimal? expected, ClinicalMeasurement? actual)
    {
        if (expected is null)
        {
            Assert.Null(actual);
        }
        else
        {
            Assert.Equal(expected.Value, Assert.IsType<ClinicalMeasurement>(actual).Value.Value);
        }
    }

    private static Guid StableBundleId(string key)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key));
        return new Guid(bytes.AsSpan(0, 16));
    }

    private static string WorkflowCode(ConsultationWorkflow workflow) => workflow switch
    {
        ConsultationWorkflow.PreConfirmation => "pre-confirmation",
        ConsultationWorkflow.InitializedDraft => "initialized-draft",
        ConsultationWorkflow.PartialCalculation => "partial-calculation",
        ConsultationWorkflow.CompleteConsultation => "complete-consultation",
        ConsultationWorkflow.IrregularDraft => "irregular-draft",
        _ => throw new ArgumentOutOfRangeException(nameof(workflow))
    };

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
