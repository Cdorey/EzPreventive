using EzNutrition.Archives.Contracts.Abstractions;
using EzNutrition.Archives.Contracts.Bundles;
using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Archives.Contracts.Metadata;
using EzNutrition.Archives.Contracts.Repositories;
using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Archives.Contracts.Serialization;
using EzNutrition.Archives.Contracts.Tests.Fixtures;
using EzNutrition.Archives.Contracts.Validation;
using EzNutrition.Archives.Contracts.ValueObjects;

namespace EzNutrition.Archives.Contracts.Tests.Tests;

/// <summary>
/// 验证档案契约在版本冲突、兼容回写和部分记录情境下的边界行为。
/// </summary>
public sealed class ContractRobustnessTests
{
    /// <summary>
    /// 验证 Bundle 会保留赋值时的资源集合快照。
    /// </summary>
    [Fact]
    public void Bundle_entries_are_isolated_from_the_source_collection()
    {
        var sourceBundle = ArchiveSamples.GetRequired("comprehensive-adult").Bundle;
        var sourceEntries = sourceBundle.Entries.ToList();
        var expectedCount = sourceEntries.Count;
        var snapshot = sourceBundle with { Entries = sourceEntries };

        sourceEntries.Clear();

        Assert.Equal(expectedCount, snapshot.Entries.Count);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<IArchiveResource>)snapshot.Entries).Clear());
    }

    /// <summary>
    /// 验证仓储当前结果可以显式表达多个版本头。
    /// </summary>
    [Fact]
    public void Current_result_preserves_branch_conflicts()
    {
        var amended = ArchiveSamples.GetRequired("amendment-chain")
            .Bundle.Entries.OfType<SoapNoteResource>()
            .Single(note => note.Metadata.Status == ResourceLifecycleStatus.Amended);
        var competingAmendment = amended with
        {
            Metadata = amended.Metadata with
            {
                VersionId = new ResourceVersionId(
                    Guid.Parse("E6F8D864-3DA5-4E3E-BEA3-2C56D950D412"))
            }
        };
        var heads = new List<IArchiveResource> { amended, competingAmendment };
        var result = new ArchiveCurrentResult { Heads = heads };

        heads.Clear();

        Assert.True(result.IsFound);
        Assert.True(result.HasConflict);
        Assert.Equal(2, result.Heads.Count);
    }

    /// <summary>
    /// 验证仓储当前结果拒绝混合逻辑资源或重复版本。
    /// </summary>
    [Fact]
    public void Current_result_rejects_invalid_head_sets()
    {
        var first = ArchiveSamples.GetRequired("comprehensive-adult").Bundle.Entries[0];
        var second = ArchiveSamples.GetRequired("minimal-anonymous").Bundle.Entries[0];

        Assert.Throws<ArgumentException>(() => new ArchiveCurrentResult
        {
            Heads = new[] { first, second }
        });
        Assert.Throws<ArgumentException>(() => new ArchiveCurrentResult
        {
            Heads = new[] { first, first }
        });
    }

    /// <summary>
    /// 验证读取结果将未知内容的回写状态与类型化档案保持在同一文档中。
    /// </summary>
    [Fact]
    public void Read_result_carries_unknown_content_round_trip_state()
    {
        var sampleDocument = ArchiveSamples.GetRequired("extensions-and-identifiers").Document;
        var document = sampleDocument with
        {
            RoundTripState = new SyntheticRoundTripState(containsUnknownContent: true)
        };
        var result = new ArchiveReadResult
        {
            Document = document,
            Validation = new ArchiveValidationResult()
        };
        var writeRequest = new ArchiveWriteRequest
        {
            Document = document,
            TargetFormat = Assert.IsType<ArchiveFormatDescriptor>(document.SourceFormat)
        };

        Assert.True(result.IsSuccess);
        Assert.True(result.ContainsUnknownContent);
        Assert.Same(document.RoundTripState, writeRequest.Document.RoundTripState);
    }

    /// <summary>
    /// 验证草稿或既往导入记录可以表达尚未形成的决定与缺失的参考数据来源。
    /// </summary>
    [Fact]
    public void Partial_assessments_can_preserve_known_content_without_fabricated_provenance()
    {
        var resources = ArchiveSamples.GetRequired("comprehensive-adult").Bundle.Entries;
        var sourceEnergy = resources.OfType<EnergyAssessmentResource>().Single();
        var undecidedEnergy = sourceEnergy with
        {
            ProfessionalDecision = null,
            ProfessionalDecisionAbsentReason = DataAbsentReasonCode.NotEstablished
        };
        var importedEnergy = sourceEnergy with
        {
            ProfessionalDecision = new ProfessionalEnergyDecision
            {
                AdoptedEnergyTarget = sourceEnergy.ProfessionalDecision!.AdoptedEnergyTarget,
                DecisionBasisAbsentReason = DataAbsentReasonCode.Unknown
            }
        };
        var dri = resources.OfType<DriAssessmentResource>().Single() with
        {
            Selector = null,
            ReferenceData = null,
            ReferenceDataAbsentReason = DataAbsentReasonCode.Unknown,
            PopulationGroup = null
        };
        var originalRecall = resources.OfType<DietaryRecallResource>().Single();
        var originalEntry = originalRecall.Meals.SelectMany(meal => meal.Entries).First();
        var entry = originalEntry with
        {
            AdoptedConsumedAmount = null,
            FoodCompositionData = null,
            FoodCompositionDataAbsentReason = DataAbsentReasonCode.Unknown
        };

        Assert.Null(undecidedEnergy.ProfessionalDecision);
        Assert.Null(importedEnergy.ProfessionalDecision?.DecisionBasis);
        Assert.Equal(DataAbsentReasonCode.Unknown, importedEnergy.ProfessionalDecision?.DecisionBasisAbsentReason);
        Assert.Null(dri.ReferenceData);
        Assert.Null(entry.AdoptedConsumedAmount);
        Assert.Equal(DataAbsentReasonCode.Unknown, entry.FoodCompositionDataAbsentReason);
    }

    /// <summary>
    /// 验证错误建立状态可以记录原因、时间和执行者。
    /// </summary>
    [Fact]
    public void Entered_in_error_status_carries_audit_context()
    {
        var source = ArchiveSamples.GetRequired("comprehensive-adult")
            .Bundle.Entries.OfType<SoapNoteResource>().Single();
        var markedAt = source.Metadata.LastModifiedAt.AddMinutes(5);
        var metadata = source.Metadata with
        {
            Status = ResourceLifecycleStatus.EnteredInError,
            EnteredInErrorReason = new Coding(
                new Uri("https://example.invalid/eznutrition-test/codes/error-reason"),
                "wrong-subject",
                display: "咨询对象关联错误"),
            EnteredInErrorReasonText = "虚构测试原因。",
            EnteredInErrorAt = markedAt,
            EnteredInErrorBy = source.Metadata.FinalizedBy
        };

        Assert.Equal(ResourceLifecycleStatus.EnteredInError, metadata.Status);
        Assert.Equal("wrong-subject", metadata.EnteredInErrorReason?.Code);
        Assert.Equal(markedAt, metadata.EnteredInErrorAt);
        Assert.NotNull(metadata.EnteredInErrorBy);
    }

    private sealed class SyntheticRoundTripState : ArchiveRoundTripState
    {
        public SyntheticRoundTripState(bool containsUnknownContent)
            : base(
                new Uri("https://example.invalid/eznutrition-test/codecs/synthetic"),
                containsUnknownContent)
        {
        }
    }
}
