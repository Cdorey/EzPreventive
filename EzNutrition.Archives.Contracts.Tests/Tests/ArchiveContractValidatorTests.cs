using EzNutrition.Archives.Contracts.Abstractions;
using EzNutrition.Archives.Contracts.Bundles;
using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Archives.Contracts.Metadata;
using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Archives.Contracts.Tests.Fixtures;
using EzNutrition.Archives.Contracts.Validation;
using EzNutrition.Archives.Contracts.ValueObjects;

namespace EzNutrition.Archives.Contracts.Tests.Tests;

/// <summary>
/// 验证格式无关档案语义校验器的结构、引用和营养规则。
/// </summary>
public sealed class ArchiveContractValidatorTests
{
    private static readonly ArchiveContractValidator Validator = new();

    /// <summary>
    /// 验证既有八个合成正向样本均没有阻断性问题。
    /// </summary>
    [Fact]
    public void All_reference_samples_have_no_validation_errors()
    {
        foreach (var sample in ArchiveSamples.All)
        {
            var result = Validator.ValidateBundle(sample.Bundle, ArchiveValidationScope.Export);
            Assert.False(
                result.HasErrors,
                $"{sample.Key}: {string.Join(" | ", result.Issues.Select(issue => issue.Code))}");
        }
    }

    /// <summary>
    /// 验证悬空引用和咨询闭包缺失会阻止导出。
    /// </summary>
    [Fact]
    public void Dangling_consultation_reference_is_an_error()
    {
        var sample = ArchiveSamples.GetRequired("comprehensive-adult");
        var consultation = sample.Bundle.Entries.OfType<ConsultationResource>().Single();
        var references = consultation.ClinicalResourceReferences.ToArray();
        references[0] = new VersionedResourceReference(
            references[0].ResourceId,
            new ResourceVersionId(Guid.Parse("90000000-0000-0000-0000-000000000001")),
            references[0].ExpectedResourceType);
        var changed = consultation with { ClinicalResourceReferences = references };
        var bundle = Replace(sample.Bundle, consultation, changed);

        var result = Validator.ValidateBundle(bundle, ArchiveValidationScope.Export);

        Assert.Contains(result.Issues, issue => issue.Code == ArchiveValidationCodes.UnresolvedReference);
        Assert.Contains(result.Issues, issue => issue.Code == ArchiveValidationCodes.ConsultationClosureMismatch);
        Assert.True(result.HasErrors);
    }

    /// <summary>
    /// 验证明确未摄入状态不能与食物摄入条目并存。
    /// </summary>
    [Fact]
    public void No_intake_status_with_food_entries_is_an_error()
    {
        var sample = ArchiveSamples.GetRequired("multi-meal-recall");
        var recall = sample.Bundle.Entries.OfType<DietaryRecallResource>().Single();
        var changed = recall with { Status = DietaryRecallStatus.NoIntake };

        var result = Validator.ValidateResource(changed, ArchiveValidationScope.Finalization);

        Assert.Contains(result.Issues, issue =>
            issue.Code == ArchiveValidationCodes.InvalidLifecycleState &&
            issue.Severity == ArchiveValidationSeverity.Error);
    }

    /// <summary>
    /// 验证餐次与全日营养素汇总不一致时会产生结构化错误。
    /// </summary>
    [Fact]
    public void Nutrient_total_mismatch_is_an_error()
    {
        var sample = ArchiveSamples.GetRequired("multi-meal-recall");
        var recall = sample.Bundle.Entries.OfType<DietaryRecallResource>().Single();
        var totals = recall.TotalNutrientSummary.ToArray();
        totals[0] = totals[0] with
        {
            Amount = new Quantity(totals[0].Amount.Value + 100m, totals[0].Amount.Unit)
        };
        var changed = recall with { TotalNutrientSummary = totals };

        var result = Validator.ValidateResource(changed, ArchiveValidationScope.Export);

        Assert.Contains(result.Issues, issue => issue.Code == ArchiveValidationCodes.NutrientAggregationMismatch);
        Assert.True(result.HasErrors);
    }

    /// <summary>
    /// 验证宏量营养素折算差异超出显式容差且无说明时会阻止导出。
    /// </summary>
    [Fact]
    public void Energy_difference_beyond_tolerance_is_an_error()
    {
        var sample = ArchiveSamples.GetRequired("multi-meal-recall");
        var recall = sample.Bundle.Entries.OfType<DietaryRecallResource>().Single();
        var consistency = Assert.IsType<DietaryEnergyConsistency>(recall.EnergyConsistency);
        var changed = recall with
        {
            EnergyConsistency = consistency with
            {
                RecordedTotalEnergy = new Quantity(
                    consistency.RecordedTotalEnergy.Value + 100m,
                    consistency.RecordedTotalEnergy.Unit)
            }
        };

        var result = Validator.ValidateResource(changed, ArchiveValidationScope.Export);

        Assert.Contains(result.Issues, issue => issue.Code == ArchiveValidationCodes.EnergyConsistencyExceeded);
        Assert.True(result.HasErrors);
    }

    /// <summary>
    /// 验证负数临床数量只触发专业复核提示，不被 Contracts 直接禁止。
    /// </summary>
    [Fact]
    public void Negative_clinical_measurement_is_a_non_blocking_warning()
    {
        var sample = ArchiveSamples.GetRequired("historical-snapshot");
        var consultation = sample.Bundle.Entries.OfType<ConsultationResource>().Single();
        var snapshot = Assert.IsType<SubjectSnapshot>(consultation.SubjectSnapshot);
        var weight = Assert.IsType<ClinicalMeasurement>(snapshot.Weight);
        var changed = consultation with
        {
            SubjectSnapshot = snapshot with
            {
                Weight = weight with
                {
                    Value = new Quantity(-54m, weight.Value.Unit)
                }
            }
        };

        var result = Validator.ValidateResource(changed, ArchiveValidationScope.Finalization);

        var issue = Assert.Single(result.Issues, item => item.Code == ArchiveValidationCodes.ClinicalValueReview);
        Assert.Equal(ArchiveValidationSeverity.Warning, issue.Severity);
        Assert.Equal(ArchiveValidationCategory.Clinical, issue.Category);
        Assert.False(result.HasErrors);
    }

    /// <summary>
    /// 验证扩展的原子值和子扩展选择具有排他性。
    /// </summary>
    [Fact]
    public void Extension_value_and_children_are_mutually_exclusive()
    {
        var sample = ArchiveSamples.GetRequired("minimal-anonymous");
        var patient = sample.Bundle.Entries.OfType<PatientResource>().Single();
        var extension = new ArchiveExtension(new Uri("https://example.invalid/extensions/conflict"))
        {
            Value = new TextArchiveValue("合成值"),
            Children =
            [
                new ArchiveExtension(new Uri("https://example.invalid/extensions/conflict/child"))
                {
                    Value = new BooleanArchiveValue(true)
                }
            ]
        };
        var changed = patient with
        {
            Metadata = patient.Metadata with { Extensions = [extension] }
        };

        var result = Validator.ValidateResource(changed, ArchiveValidationScope.DraftSave);

        Assert.Contains(result.Issues, issue => issue.Code == ArchiveValidationCodes.ExtensionChoiceConflict);
        Assert.True(result.HasErrors);
    }

    /// <summary>
    /// 验证修订缺少被替代版本时会产生结构化错误。
    /// </summary>
    [Fact]
    public void Amended_resource_requires_a_superseded_version()
    {
        var sample = ArchiveSamples.GetRequired("amendment-chain");
        var amended = sample.Bundle.Entries
            .OfType<SoapNoteResource>()
            .Single(resource => resource.Metadata.Status == ResourceLifecycleStatus.Amended);
        var changed = amended with
        {
            Metadata = amended.Metadata with { Supersedes = null }
        };

        var result = Validator.ValidateResource(changed, ArchiveValidationScope.Export);

        Assert.Contains(result.Issues, issue => issue.Code == ArchiveValidationCodes.InvalidRevisionRelationship);
        Assert.True(result.HasErrors);
    }

    /// <summary>
    /// 验证错误消息不回显 SOAP 原文。
    /// </summary>
    [Fact]
    public void Validation_messages_do_not_echo_sensitive_resource_content()
    {
        const string sensitiveMarker = "SENSITIVE-SYNTHETIC-SOAP-MARKER";
        var sample = ArchiveSamples.GetRequired("comprehensive-adult");
        var soap = sample.Bundle.Entries.OfType<SoapNoteResource>().Single();
        var changed = soap with
        {
            Subjective = sensitiveMarker,
            Metadata = soap.Metadata with
            {
                FinalizedAt = null,
                FinalizedBy = null
            }
        };

        var result = Validator.ValidateResource(changed, ArchiveValidationScope.Export);

        Assert.NotEmpty(result.Issues);
        Assert.DoesNotContain(result.Issues, issue =>
            issue.Message.Contains(sensitiveMarker, StringComparison.Ordinal));
    }

    /// <summary>
    /// 验证已建立但不含身份或缺失原因的主体引用会被拒绝。
    /// </summary>
    [Fact]
    public void Empty_actor_reference_is_an_error()
    {
        var sample = ArchiveSamples.GetRequired("comprehensive-adult");
        var consultation = sample.Bundle.Entries.OfType<ConsultationResource>().Single();
        var changed = consultation with { ServiceProvider = new ActorReference() };

        var result = Validator.ValidateResource(changed, ArchiveValidationScope.Export);

        Assert.Contains(result.Issues, issue =>
            issue.Code == ArchiveValidationCodes.RequiredSemanticValueMissing &&
            issue.Path?.Value.EndsWith("/ServiceProvider", StringComparison.Ordinal) == true);
        Assert.True(result.HasErrors);
    }

    /// <summary>
    /// 验证主体身份不能与其缺失原因同时存在。
    /// </summary>
    [Fact]
    public void Actor_identity_and_absent_reason_are_mutually_exclusive()
    {
        var sample = ArchiveSamples.GetRequired("comprehensive-adult");
        var consultation = sample.Bundle.Entries.OfType<ConsultationResource>().Single();
        var provider = Assert.IsType<ActorReference>(consultation.ServiceProvider);
        var changed = consultation with
        {
            ServiceProvider = provider with { AbsentReason = DataAbsentReasonCode.Unknown }
        };

        var result = Validator.ValidateResource(changed, ArchiveValidationScope.Export);

        Assert.Contains(result.Issues, issue =>
            issue.Code == ArchiveValidationCodes.ValueAndAbsentReasonConflict &&
            issue.Path?.Value.EndsWith("/ServiceProvider", StringComparison.Ordinal) == true);
        Assert.True(result.HasErrors);
    }

    private static ArchiveBundle Replace<TResource>(
        ArchiveBundle bundle,
        TResource original,
        TResource replacement)
        where TResource : class, IArchiveResource => bundle with
        {
            Entries = bundle.Entries
                .Select(resource => ReferenceEquals(resource, original) ? replacement : resource)
                .ToArray()
        };
}
