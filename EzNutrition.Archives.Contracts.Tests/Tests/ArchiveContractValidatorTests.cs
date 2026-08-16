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
    /// 验证全部合成正向样本均没有阻断性问题。
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
    /// 验证不同数据口径产生的能量差异会被忠实保留，而不由档案校验器解释。
    /// </summary>
    [Fact]
    public void Recorded_and_derived_energy_difference_is_preserved_without_archive_issue()
    {
        var sample = ArchiveSamples.GetRequired("multi-meal-recall");
        var recall = sample.Bundle.Entries.OfType<DietaryRecallResource>().Single();
        var consistency = Assert.IsType<DietaryEnergyConsistency>(recall.EnergyConsistency);
        var changed = recall with
        {
            EnergyConsistency = consistency with
            {
                Method = consistency.Method with
                {
                    Method = new Coding(
                        consistency.Method.Method.System,
                        "external-energy-comparison",
                        display: "外部能量比较方法")
                },
                MacronutrientDerivedEnergy = new Quantity(
                    consistency.MacronutrientDerivedEnergy.Value - 100m,
                    consistency.MacronutrientDerivedEnergy.Unit),
                AllowedDifference = new Quantity(0m, consistency.RecordedTotalEnergy.Unit)
            }
        };

        var result = Validator.ValidateResource(changed, ArchiveValidationScope.Export);

        Assert.Empty(result.Issues);
    }

    /// <summary>
    /// 验证档案校验器不解释负数临床数量的合理性。
    /// </summary>
    [Fact]
    public void Negative_clinical_measurement_is_preserved_without_archive_issue()
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

        Assert.Empty(result.Issues);
    }

    /// <summary>
    /// 验证空 SOAP 内容可作为忠实事实保存，不由档案校验器产生临床提示。
    /// </summary>
    [Fact]
    public void Empty_soap_content_is_preserved_without_archive_issue()
    {
        var sample = ArchiveSamples.GetRequired("comprehensive-adult");
        var soap = sample.Bundle.Entries.OfType<SoapNoteResource>().Single();
        var changed = soap with
        {
            Subjective = null,
            Objective = null,
            Assessment = null,
            Plan = null
        };

        var result = Validator.ValidateResource(changed, ArchiveValidationScope.DraftSave);

        Assert.Empty(result.Issues);
    }

    /// <summary>
    /// 验证明确声明的确定性折算结果仍必须能够由档案内组成项复算。
    /// </summary>
    [Fact]
    public void Declared_deterministic_energy_result_must_remain_reproducible()
    {
        var sample = ArchiveSamples.GetRequired("multi-meal-recall");
        var recall = sample.Bundle.Entries.OfType<DietaryRecallResource>().Single();
        var consistency = Assert.IsType<DietaryEnergyConsistency>(recall.EnergyConsistency);
        var changed = recall with
        {
            EnergyConsistency = consistency with
            {
                MacronutrientDerivedEnergy = new Quantity(
                    consistency.MacronutrientDerivedEnergy.Value - 100m,
                    consistency.MacronutrientDerivedEnergy.Unit)
            }
        };

        var result = Validator.ValidateResource(changed, ArchiveValidationScope.Export);

        var issue = Assert.Single(result.Issues, item =>
            item.Code == ArchiveValidationCodes.NutrientAggregationMismatch &&
            item.Path?.Value.EndsWith("/MacronutrientDerivedEnergy", StringComparison.Ordinal) == true);
        Assert.Equal(ArchiveValidationCategory.Integrity, issue.Category);
        Assert.True(result.HasErrors);
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

    /// <summary>
    /// 验证草稿报告可以尚未复核和渲染，但仍应保留作者事实。
    /// </summary>
    [Fact]
    public void Draft_report_can_remain_unreviewed_and_unrendered()
    {
        var report = TeachingReport();
        var author = report.Participants.Single(item => item.Function.Code == "author");
        var changed = report with
        {
            Metadata = report.Metadata with
            {
                Status = ResourceLifecycleStatus.Draft,
                FinalizedAt = null,
                FinalizedBy = null
            },
            RenderedArtifact = null,
            Participants = [author]
        };

        var result = Validator.ValidateResource(changed, ArchiveValidationScope.DraftSave);

        Assert.Empty(result.Issues);
    }

    /// <summary>
    /// 验证作者具备业务资格时可以自行签发，不强制虚构另一名复核者。
    /// </summary>
    [Fact]
    public void Report_author_may_also_be_the_finalizer()
    {
        var report = TeachingReport();
        var teacher = report.Participants.Single(item => item.Function.Code == "reviewer").Actor;
        var author = report.Participants.Single(item => item.Function.Code == "author") with
        {
            Actor = teacher
        };
        var changed = report with
        {
            Metadata = report.Metadata with { FinalizedBy = author.Actor },
            Participants = [author]
        };

        var result = Validator.ValidateResource(changed, ArchiveValidationScope.Finalization);

        Assert.Empty(result.Issues);
    }

    /// <summary>
    /// 验证修订报告可以保留早于当前版本建立时间的原始参与事实。
    /// </summary>
    [Fact]
    public void Amended_report_can_preserve_participation_from_an_earlier_version()
    {
        var report = TeachingReport();
        var changed = report with
        {
            Metadata = report.Metadata with
            {
                RevisionNumber = new RevisionNumber(2),
                Status = ResourceLifecycleStatus.Amended,
                CreatedAt = report.Metadata.CreatedAt.AddMinutes(30),
                Supersedes = new VersionedResourceReference(
                    report.Metadata.ResourceId,
                    new ResourceVersionId(Guid.Parse("90000000-0000-0000-0000-000000000003")),
                    ArchiveResourceTypes.NutritionReport)
            }
        };

        var result = Validator.ValidateResource(changed, ArchiveValidationScope.Finalization);

        Assert.Empty(result.Issues);
    }

    /// <summary>
    /// 验证正式报告必须绑定用户实际看到的确切渲染产物。
    /// </summary>
    [Fact]
    public void Final_report_requires_a_rendered_artifact_fingerprint()
    {
        var changed = TeachingReport() with { RenderedArtifact = null };

        var result = Validator.ValidateResource(changed, ArchiveValidationScope.Finalization);

        Assert.Contains(result.Issues, issue =>
            issue.Code == ArchiveValidationCodes.RequiredSemanticValueMissing &&
            issue.Path?.Value.EndsWith("/RenderedArtifact", StringComparison.Ordinal) == true);
        Assert.True(result.HasErrors);
    }

    /// <summary>
    /// 验证报告输入必须解析到 Bundle 中的确切资源版本。
    /// </summary>
    [Fact]
    public void Report_input_must_resolve_to_an_exact_resource_version()
    {
        var sample = ArchiveSamples.GetRequired("teaching-report");
        var report = sample.Bundle.Entries.OfType<NutritionReportResource>().Single();
        var input = Assert.Single(report.InputResourceReferences);
        var changed = report with
        {
            InputResourceReferences =
            [
                new VersionedResourceReference(
                    input.ResourceId,
                    new ResourceVersionId(Guid.Parse("90000000-0000-0000-0000-000000000002")),
                    input.ExpectedResourceType)
            ]
        };
        var bundle = Replace(sample.Bundle, report, changed);

        var result = Validator.ValidateBundle(bundle, ArchiveValidationScope.Export);

        Assert.Contains(result.Issues, issue =>
            issue.Code == ArchiveValidationCodes.UnresolvedReference &&
            issue.Path?.Value.Contains("/InputResourceReferences/0", StringComparison.Ordinal) == true);
        Assert.True(result.HasErrors);
    }

    /// <summary>
    /// 验证参与者的机构快照也必须包含身份或明确的缺失原因。
    /// </summary>
    [Fact]
    public void Report_participant_organization_is_validated()
    {
        var report = TeachingReport();
        var participants = report.Participants.ToArray();
        participants[0] = participants[0] with
        {
            Actor = participants[0].Actor with { Organization = new ActorReference() }
        };
        var changed = report with { Participants = participants };

        var result = Validator.ValidateResource(changed, ArchiveValidationScope.Export);

        Assert.Contains(result.Issues, issue =>
            issue.Code == ArchiveValidationCodes.RequiredSemanticValueMissing &&
            issue.Path?.Value.EndsWith("/Participants/0/Actor/Organization", StringComparison.Ordinal) == true);
        Assert.True(result.HasErrors);
    }

    /// <summary>
    /// 验证行为时机构只保存一层快照，避免循环对象和无边界组织树。
    /// </summary>
    [Fact]
    public void Actor_organization_snapshot_cannot_nest_another_organization()
    {
        var report = TeachingReport();
        var participants = report.Participants.ToArray();
        var organization = Assert.IsType<ActorReference>(participants[0].Actor.Organization);
        participants[0] = participants[0] with
        {
            Actor = participants[0].Actor with
            {
                Organization = organization with
                {
                    Organization = new ActorReference { Display = "虚构上级机构" }
                }
            }
        };
        var changed = report with { Participants = participants };

        var result = Validator.ValidateResource(changed, ArchiveValidationScope.Export);

        Assert.Contains(result.Issues, issue =>
            issue.Code == ArchiveValidationCodes.InvalidTechnicalValue &&
            issue.Path?.Value.EndsWith(
                "/Participants/0/Actor/Organization/Organization",
                StringComparison.Ordinal) == true);
        Assert.True(result.HasErrors);
    }

    /// <summary>
    /// 验证报告不能把自身当前版本列为内容来源。
    /// </summary>
    [Fact]
    public void Report_cannot_use_its_current_version_as_an_input()
    {
        var report = TeachingReport();
        var changed = report with
        {
            InputResourceReferences =
            [
                new VersionedResourceReference(
                    report.Metadata.ResourceId,
                    report.Metadata.VersionId,
                    ArchiveResourceTypes.NutritionReport)
            ]
        };

        var result = Validator.ValidateResource(changed, ArchiveValidationScope.Export);

        Assert.Contains(result.Issues, issue =>
            issue.Code == ArchiveValidationCodes.InvalidTechnicalValue &&
            issue.Path?.Value.EndsWith("/InputResourceReferences/0", StringComparison.Ordinal) == true);
        Assert.True(result.HasErrors);
    }

    private static NutritionReportResource TeachingReport() =>
        ArchiveSamples.GetRequired("teaching-report")
            .Bundle.Entries.OfType<NutritionReportResource>().Single();

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
