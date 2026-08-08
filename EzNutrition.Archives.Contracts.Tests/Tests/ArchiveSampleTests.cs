using EzNutrition.Archives.Contracts.Abstractions;
using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Archives.Contracts.Metadata;
using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Archives.Contracts.Tests.Fixtures;
using EzNutrition.Archives.Contracts.ValueObjects;

namespace EzNutrition.Archives.Contracts.Tests.Tests;

/// <summary>
/// 验证合成档案样本本身能够作为稳定、闭合且可复核的测试基线。
/// </summary>
public sealed class ArchiveSampleTests
{
    /// <summary>
    /// 验证首轮样本数量、名称和 Bundle 标识均保持稳定且唯一。
    /// </summary>
    [Fact]
    public void Catalog_contains_eight_distinct_samples()
    {
        Assert.Equal(8, ArchiveSamples.All.Count);
        Assert.Equal(8, ArchiveSamples.All.Select(sample => sample.Key).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(8, ArchiveSamples.All.Select(sample => sample.Bundle.BundleId).Distinct().Count());
        Assert.All(ArchiveSamples.All, sample => Assert.False(string.IsNullOrWhiteSpace(sample.Description)));
    }

    /// <summary>
    /// 验证每个样本的资源版本身份唯一，且生命周期时间不存在倒置。
    /// </summary>
    [Fact]
    public void Every_sample_has_unique_versions_and_ordered_metadata_times()
    {
        foreach (var sample in ArchiveSamples.All)
        {
            Assert.NotEmpty(sample.Bundle.Entries);
            Assert.Equal(
                sample.Bundle.Entries.Count,
                sample.Bundle.Entries.Select(resource => resource.Metadata.VersionId).Distinct().Count());

            foreach (var resource in sample.Bundle.Entries)
            {
                Assert.True(
                    resource.Metadata.CreatedAt <= resource.Metadata.LastModifiedAt,
                    $"样本 {sample.Key} 的 {resource.ResourceType} 修改时间早于建立时间。");

                if (resource.Metadata.Status == ResourceLifecycleStatus.Draft)
                {
                    Assert.Null(resource.Metadata.FinalizedAt);
                    Assert.Null(resource.Metadata.FinalizedBy);
                }
                else
                {
                    Assert.NotNull(resource.Metadata.FinalizedAt);
                    Assert.NotNull(resource.Metadata.FinalizedBy);
                }
            }
        }
    }

    /// <summary>
    /// 验证资源声明的稳定类型代码与其实际契约类型一致。
    /// </summary>
    [Fact]
    public void Every_resource_reports_the_expected_type_code()
    {
        foreach (var resource in ArchiveSamples.All.SelectMany(sample => sample.Bundle.Entries))
        {
            var expected = resource switch
            {
                PatientResource => ArchiveResourceTypes.Patient,
                ConsultationResource => ArchiveResourceTypes.Consultation,
                EnergyAssessmentResource => ArchiveResourceTypes.EnergyAssessment,
                DriAssessmentResource => ArchiveResourceTypes.DriAssessment,
                DietaryRecallResource => ArchiveResourceTypes.DietaryRecall,
                SoapNoteResource => ArchiveResourceTypes.SoapNote,
                _ => throw new Xunit.Sdk.XunitException($"未识别的样本资源类型：{resource.GetType().FullName}")
            };

            Assert.Equal(expected, resource.ResourceType);
        }
    }

    /// <summary>
    /// 验证样本中的逻辑引用和确切版本引用均可在同一 Bundle 内解析。
    /// </summary>
    [Fact]
    public void Every_internal_reference_resolves_inside_its_bundle()
    {
        foreach (var sample in ArchiveSamples.All)
        {
            foreach (var resource in sample.Bundle.Entries)
            {
                foreach (var reference in GetLogicalReferences(resource))
                {
                    var matches = sample.Bundle.Entries
                        .Where(candidate => candidate.Metadata.ResourceId == reference.ResourceId)
                        .ToArray();

                    Assert.True(matches.Length > 0, $"样本 {sample.Key} 存在无法解析的逻辑引用 {reference.ResourceId}。");
                    if (reference.ExpectedResourceType is { } expectedType)
                    {
                        Assert.All(matches, match => Assert.Equal(expectedType, match.ResourceType));
                    }
                }

                foreach (var reference in GetVersionedReferences(resource))
                {
                    var match = Assert.Single(sample.Bundle.Entries.Where(candidate =>
                        candidate.Metadata.ResourceId == reference.ResourceId &&
                        candidate.Metadata.VersionId == reference.VersionId));

                    if (reference.ExpectedResourceType is { } expectedType)
                    {
                        Assert.Equal(expectedType, match.ResourceType);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 验证完整成人样本覆盖首期定义的全部六类资源。
    /// </summary>
    [Fact]
    public void Comprehensive_sample_contains_every_initial_resource_type()
    {
        var resources = ArchiveSamples.GetRequired("comprehensive-adult").Bundle.Entries;

        Assert.Single(resources.OfType<PatientResource>());
        Assert.Single(resources.OfType<ConsultationResource>());
        Assert.Single(resources.OfType<EnergyAssessmentResource>());
        Assert.Single(resources.OfType<DriAssessmentResource>());
        Assert.Single(resources.OfType<DietaryRecallResource>());
        Assert.Single(resources.OfType<SoapNoteResource>());
    }

    /// <summary>
    /// 验证多餐次样本的食物贡献、餐次汇总和全日汇总逐级相等。
    /// </summary>
    [Fact]
    public void Multi_meal_sample_nutrient_totals_reconcile_at_every_level()
    {
        var recall = ArchiveSamples.GetRequired("multi-meal-recall")
            .Bundle.Entries.OfType<DietaryRecallResource>().Single();

        foreach (var meal in recall.Meals)
        {
            foreach (var summary in meal.NutrientSummary)
            {
                var contributions = meal.Entries
                    .SelectMany(entry => entry.NutrientContributions)
                    .Where(amount => amount.Nutrient.HasSameIdentity(summary.Nutrient))
                    .ToArray();

                Assert.NotEmpty(contributions);
                Assert.All(contributions, amount => Assert.True(amount.Amount.Unit.HasSameIdentity(summary.Amount.Unit)));
                Assert.Equal(summary.Amount.Value, contributions.Sum(amount => amount.Amount.Value));
            }
        }

        foreach (var total in recall.TotalNutrientSummary)
        {
            var mealAmounts = recall.Meals
                .SelectMany(meal => meal.NutrientSummary)
                .Where(amount => amount.Nutrient.HasSameIdentity(total.Nutrient))
                .ToArray();

            Assert.Equal(total.Amount.Value, mealAmounts.Sum(amount => amount.Amount.Value));
            Assert.All(mealAmounts, amount => Assert.True(amount.Amount.Unit.HasSameIdentity(total.Amount.Unit)));
        }
    }

    /// <summary>
    /// 验证宏量营养素折算能量与样本保存的一致性快照相符。
    /// </summary>
    [Fact]
    public void Multi_meal_sample_macronutrients_reconcile_with_energy_snapshot()
    {
        var recall = ArchiveSamples.GetRequired("multi-meal-recall")
            .Bundle.Entries.OfType<DietaryRecallResource>().Single();
        var consistency = Assert.IsType<DietaryEnergyConsistency>(recall.EnergyConsistency);

        var protein = NutrientValue(recall, "protein");
        var fat = NutrientValue(recall, "fat");
        var carbohydrate = NutrientValue(recall, "carbohydrate");
        var calculatedEnergy = (protein * 4m) + (fat * 9m) + (carbohydrate * 4m);

        Assert.Equal(calculatedEnergy, consistency.MacronutrientDerivedEnergy.Value);
        Assert.True(consistency.RecordedTotalEnergy.Unit.HasSameIdentity(consistency.MacronutrientDerivedEnergy.Unit));
        Assert.True(consistency.RecordedTotalEnergy.Unit.HasSameIdentity(consistency.AllowedDifference.Unit));
        Assert.True(
            Math.Abs(consistency.RecordedTotalEnergy.Value - consistency.MacronutrientDerivedEnergy.Value) <=
            consistency.AllowedDifference.Value);
    }

    /// <summary>
    /// 验证咨询历史显示快照不会被患者当前显示资料替代。
    /// </summary>
    [Fact]
    public void Historical_snapshot_keeps_the_consultation_time_display()
    {
        var entries = ArchiveSamples.GetRequired("historical-snapshot").Bundle.Entries;
        var patient = entries.OfType<PatientResource>().Single();
        var consultation = entries.OfType<ConsultationResource>().Single();

        Assert.Equal("虚构当前称呼", patient.Names.Single().Text);
        Assert.Equal("虚构既往称呼", consultation.SubjectSnapshot?.IdentityDisplay);
        Assert.NotEqual(patient.Names.Single().Text, consultation.SubjectSnapshot?.IdentityDisplay);
    }

    /// <summary>
    /// 验证契约不会因行政性别与特殊生理状态表面矛盾而拒绝保存资料。
    /// </summary>
    [Fact]
    public void Special_physiology_sample_preserves_professional_input_without_frontend_enumeration()
    {
        var consultation = ArchiveSamples.GetRequired("special-physiology")
            .Bundle.Entries.OfType<ConsultationResource>().Single();
        var snapshot = Assert.IsType<SubjectSnapshot>(consultation.SubjectSnapshot);

        Assert.Equal("male", snapshot.AdministrativeSex?.Code);
        Assert.Contains(snapshot.PhysiologicalStates, state => state.Code == "pregnancy-third-trimester");
    }

    /// <summary>
    /// 验证修订版本保持逻辑资源身份，并明确引用被替代的正式版本。
    /// </summary>
    [Fact]
    public void Amendment_sample_has_an_explicit_two_version_chain()
    {
        var notes = ArchiveSamples.GetRequired("amendment-chain")
            .Bundle.Entries.OfType<SoapNoteResource>()
            .OrderBy(note => note.Metadata.RevisionNumber.Value)
            .ToArray();

        var original = notes[0];
        var amended = notes[1];

        Assert.Equal(original.Metadata.ResourceId, amended.Metadata.ResourceId);
        Assert.NotEqual(original.Metadata.VersionId, amended.Metadata.VersionId);
        Assert.Equal(ResourceLifecycleStatus.Final, original.Metadata.Status);
        Assert.Equal(ResourceLifecycleStatus.Amended, amended.Metadata.Status);
        Assert.Equal(1, original.Metadata.RevisionNumber.Value);
        Assert.Equal(2, amended.Metadata.RevisionNumber.Value);
        Assert.Equal(original.Metadata.ResourceId, amended.Metadata.Supersedes?.ResourceId);
        Assert.Equal(original.Metadata.VersionId, amended.Metadata.Supersedes?.VersionId);
    }

    /// <summary>
    /// 验证测试业务标识均显式标记为合成数据。
    /// </summary>
    [Fact]
    public void Every_business_identifier_is_explicitly_synthetic()
    {
        var identifiers = ArchiveSamples.All
            .SelectMany(sample => sample.Bundle.Entries)
            .OfType<PatientResource>()
            .SelectMany(patient => patient.BusinessIdentifiers)
            .Select(identifier => identifier.Value)
            .ToArray();

        Assert.NotEmpty(identifiers);
        Assert.All(identifiers, value => Assert.StartsWith("SYNTHETIC-", value));
    }

    private static decimal NutrientValue(DietaryRecallResource recall, string code) =>
        recall.TotalNutrientSummary.Single(amount => amount.Nutrient.Code == code).Amount.Value;

    private static IEnumerable<LogicalResourceReference> GetLogicalReferences(IArchiveResource resource)
    {
        switch (resource)
        {
            case ConsultationResource consultation:
                yield return consultation.SubjectReference;
                break;
            case EnergyAssessmentResource energyAssessment:
                yield return energyAssessment.SubjectReference;
                break;
            case DriAssessmentResource driAssessment:
                yield return driAssessment.SubjectReference;
                break;
            case DietaryRecallResource dietaryRecall:
                yield return dietaryRecall.SubjectReference;
                break;
            case SoapNoteResource soapNote:
                yield return soapNote.SubjectReference;
                break;
        }
    }

    private static IEnumerable<VersionedResourceReference> GetVersionedReferences(IArchiveResource resource)
    {
        if (resource.Metadata.BasedOn is { } basedOn)
        {
            yield return basedOn;
        }

        if (resource.Metadata.Supersedes is { } supersedes)
        {
            yield return supersedes;
        }

        switch (resource)
        {
            case ConsultationResource consultation:
                foreach (var reference in consultation.ClinicalResourceReferences)
                {
                    yield return reference;
                }

                break;
            case EnergyAssessmentResource { ConsultationReference: { } reference }:
                yield return reference;
                break;
            case DriAssessmentResource { ConsultationReference: { } reference }:
                yield return reference;
                break;
            case DietaryRecallResource { ConsultationReference: { } reference }:
                yield return reference;
                break;
            case SoapNoteResource { ConsultationReference: { } reference }:
                yield return reference;
                break;
        }
    }
}
