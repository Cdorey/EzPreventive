using EzNutrition.Application.Archives;
using EzNutrition.Application.Consultations;
using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Archives.Contracts.Serialization;
using EzNutrition.Archives.Contracts.Validation;
using EzNutrition.Archives.Contracts.ValueObjects;
using EzNutrition.Archives.Xml;
using EzNutrition.Client.Tests.Fixtures;
using EzNutrition.Domain.Assessments;

namespace EzNutrition.Client.Tests.Tests;

/// <summary>
/// 验证当前注册量表从运行态到档案契约、再经 XML 编解码后的语义保真度。
/// </summary>
public sealed class AssessmentArchiveSemanticRoundTripTests
{
    private static readonly ApplicationIdentity SourceApplication = new(
        new Uri("https://example.invalid/applications/assessment-archive-semantics"),
        "量表档案语义测试",
        "2.1.0.0");

    private static readonly ArchiveContractAssembler Assembler = new(SourceApplication);
    private static readonly ArchiveContractValidator Validator = new();

    public static IEnumerable<object[]> InstrumentCases() =>
        AssessmentArchiveScenarioCatalog.InstrumentCodes.Select(code => new object[] { code });

    [Theory]
    [MemberData(nameof(InstrumentCases))]
    public async Task Generated_scale_runs_preserve_semantics_through_contract_and_xml(
        string instrumentCode)
    {
        var scenarios = AssessmentArchiveScenarioCatalog.Create(instrumentCode);
        Assert.Equal(AssessmentArchiveScenarioCatalog.TargetCount(instrumentCode), scenarios.Count);

        foreach (var scenario in scenarios)
        {
            try
            {
                await AssertRoundTripAsync(scenario);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"量表档案场景 {scenario.Id} 未能保持语义一致。",
                    exception);
            }
        }
    }

    [Theory]
    [MemberData(nameof(InstrumentCases))]
    public async Task Incomplete_scale_drafts_preserve_answers_and_not_established_total(
        string instrumentCode)
    {
        var scenario = AssessmentArchiveScenarioCatalog.Create(instrumentCode)[0];
        var answerToRemove = scenario.Run.Definition.Items.Last(item =>
            scenario.Run.Evaluation.ApplicableItemCodes.Contains(item.Code)
            && scenario.Run.Answers.ContainsKey(item.Code));

        Assert.True(scenario.Run.ClearAnswer(
            answerToRemove.Code,
            scenario.Run.LastModifiedAt.AddMinutes(1)));
        Assert.False(scenario.Run.Evaluation.IsComplete);
        Assert.NotEmpty(scenario.Run.Answers);
        await AssertRoundTripAsync(scenario with
        {
            Id = $"{instrumentCode}-incomplete-draft"
        });
    }

    [Theory]
    [MemberData(nameof(InstrumentCases))]
    public void Generated_catalog_covers_answerable_items_options_and_interpretations(
        string instrumentCode)
    {
        var scenarios = AssessmentArchiveScenarioCatalog.Create(instrumentCode);
        var definition = Assert.Single(
            scenarios.Select(scenario => scenario.Run.Definition).Distinct());
        var expectedAnswerableItems = definition.Items
            .Where(item => instrumentCode != "mna-sf" || item.Code != "bmi")
            .ToArray();
        var answeredItemCodes = scenarios
            .SelectMany(scenario => scenario.Run.Answers.Keys)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            expectedAnswerableItems.Select(item => item.Code).Order(StringComparer.Ordinal),
            answeredItemCodes);

        foreach (var item in expectedAnswerableItems)
        {
            if (item.ResponseType == NutritionAssessmentResponseType.Decimal)
            {
                var values = scenarios
                    .Select(scenario => scenario.Run.GetDecimalAnswer(item.Code))
                    .Where(value => value is not null)
                    .Select(value => value!.Value)
                    .Distinct()
                    .ToArray();
                Assert.True(
                    values.Length >= 3,
                    $"量表 {instrumentCode} 的数值题 {item.Code} 只覆盖了 {values.Length} 个不同值。");
                continue;
            }

            var selectedOptionCodes = scenarios
                .SelectMany(scenario => SelectedOptionCodes(scenario.Run, item))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(
                item.Options.Select(option => option.Code).Order(StringComparer.Ordinal),
                selectedOptionCodes);
        }

        var actualInterpretations = scenarios
            .Select(scenario => scenario.Run.Evaluation.Interpretation?.Code)
            .Where(code => code is not null)
            .Select(code => code!)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            ExpectedInterpretations(instrumentCode).Order(StringComparer.Ordinal),
            actualInterpretations);
    }

    private static async Task AssertRoundTripAsync(AssessmentArchiveScenario scenario)
    {
        var expected = AssessmentSemantics.FromRun(scenario.Run, SourceApplication);
        var source = Assembler.CreateDocument(
            scenario.Workspace,
            scenario.Run.LastModifiedAt.AddMinutes(1));
        var sourceScale = Assert.Single(source.Bundle.Entries.OfType<NutritionScaleAssessmentResource>());
        AssertEquivalent(expected, AssessmentSemantics.FromResource(sourceScale));

        var codec = new XmlArchiveCodec(Validator);
        await using var stream = new MemoryStream();
        var write = await codec.WriteAsync(new ArchiveWriteRequest
        {
            Document = source,
            TargetFormat = XmlArchiveFormat.Current
        }, stream);
        Assert.True(
            write.IsSuccess,
            string.Join(" | ", write.Validation.Issues.Select(issue => issue.Code)));

        stream.Position = 0;
        var read = await codec.ReadAsync(stream);
        Assert.True(
            read.IsSuccess,
            string.Join(" | ", read.Validation.Issues.Select(issue => issue.Code)));
        Assert.False(read.ContainsUnknownContent);
        var restoredScale = Assert.Single(
            read.Document!.Bundle.Entries.OfType<NutritionScaleAssessmentResource>());
        AssertEquivalent(expected, AssessmentSemantics.FromResource(restoredScale));
    }

    private static IEnumerable<string> SelectedOptionCodes(
        NutritionAssessmentRun run,
        NutritionAssessmentItem item)
    {
        if (!run.Answers.TryGetValue(item.Code, out var answer))
        {
            return [];
        }

        return answer switch
        {
            NutritionAssessmentSingleChoiceAnswer single => [single.OptionCode],
            NutritionAssessmentMultipleChoiceAnswer multiple => multiple.OptionCodes,
            _ => []
        };
    }

    private static IReadOnlyList<string> ExpectedInterpretations(string instrumentCode) =>
        instrumentCode switch
        {
            "nrs-2002" => ["no-current-nutritional-risk", "nutritional-risk"],
            "must" => ["low-risk", "medium-risk", "high-risk"],
            "mna-sf" => ["malnutrition-risk", "no-malnutrition-risk"],
            "sga" =>
            [
                "well-nourished",
                "mild-to-moderate-malnutrition",
                "severe-malnutrition"
            ],
            "sga-chas-2020" =>
            [
                "normal-nutritional-status",
                "mild-to-moderate-malnutrition",
                "severe-malnutrition"
            ],
            "pg-sga" =>
            [
                "well-nourished",
                "suspected-or-mild-malnutrition",
                "moderate-malnutrition",
                "severe-malnutrition"
            ],
            "ws-t-552-elderly-malnutrition-risk" =>
            [
                "no-malnutrition-risk",
                "good-nutritional-status",
                "possible-overweight-obese-malnutrition-or-risk",
                "malnutrition-risk",
                "malnutrition"
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(instrumentCode))
        };

    private static void AssertEquivalent(
        AssessmentSemantics expected,
        AssessmentSemantics actual)
    {
        Assert.Equal(expected.ResourceId, actual.ResourceId);
        Assert.Equal(expected.ResourceVersionId, actual.ResourceVersionId);
        Assert.Equal(expected.EffectiveAt, actual.EffectiveAt);
        Assert.Equal(expected.Instrument, actual.Instrument);
        Assert.Equal(expected.InstrumentVersion, actual.InstrumentVersion);
        Assert.Equal(expected.DefinitionUri, actual.DefinitionUri);
        Assert.Equal(expected.DefinitionVersion, actual.DefinitionVersion);
        Assert.Equal(expected.ScoringMethod, actual.ScoringMethod);
        Assert.Equal(expected.ScoringImplementation, actual.ScoringImplementation);
        Assert.Equal(expected.TotalScore, actual.TotalScore);
        Assert.Equal(expected.TotalScoreAbsentReason, actual.TotalScoreAbsentReason);
        Assert.Equal(expected.Interpretation, actual.Interpretation);
        Assert.Equal(expected.Performer, actual.Performer);
        Assert.Equal(expected.Responses.Count, actual.Responses.Count);
        for (var index = 0; index < expected.Responses.Count; index++)
        {
            var expectedResponse = expected.Responses[index];
            var actualResponse = actual.Responses[index];
            Assert.Equal(expectedResponse.Item, actualResponse.Item);
            Assert.Equal(expectedResponse.ScoreContribution, actualResponse.ScoreContribution);
            Assert.Equal(expectedResponse.Answer.Kind, actualResponse.Answer.Kind);
            Assert.Equal(expectedResponse.Answer.DecimalValue, actualResponse.Answer.DecimalValue);
            Assert.Equal(
                expectedResponse.Answer.Codings.ToArray(),
                actualResponse.Answer.Codings.ToArray());
        }

        Assert.Equal(expected.DerivedResults.ToArray(), actual.DerivedResults.ToArray());
    }

    private sealed record AssessmentSemantics
    {
        public required Guid ResourceId { get; init; }

        public required Guid ResourceVersionId { get; init; }

        public required DateTimeOffset EffectiveAt { get; init; }

        public required CodingSemantics Instrument { get; init; }

        public required string InstrumentVersion { get; init; }

        public required string DefinitionUri { get; init; }

        public required string DefinitionVersion { get; init; }

        public required CodingSemantics ScoringMethod { get; init; }

        public required ApplicationSemantics ScoringImplementation { get; init; }

        public required IReadOnlyList<ResponseSemantics> Responses { get; init; }

        public required IReadOnlyList<DerivedResultSemantics> DerivedResults { get; init; }

        public decimal? TotalScore { get; init; }

        public DataAbsentReasonCode? TotalScoreAbsentReason { get; init; }

        public CodingSemantics? Interpretation { get; init; }

        public PerformerSemantics? Performer { get; init; }

        public static AssessmentSemantics FromRun(
            NutritionAssessmentRun run,
            ApplicationIdentity sourceApplication)
        {
            var definition = run.Definition;
            return new AssessmentSemantics
            {
                ResourceId = run.ArchiveIdentity.ResourceId.Value,
                ResourceVersionId = run.ArchiveIdentity.VersionId.Value,
                EffectiveAt = run.CompletedAt ?? run.LastModifiedAt,
                Instrument = ExpectedCoding(
                    definition,
                    definition.Code,
                    definition.DisplayName),
                InstrumentVersion = definition.Version,
                DefinitionUri = definition.DefinitionUri.AbsoluteUri,
                DefinitionVersion = definition.Version,
                ScoringMethod = ExpectedCoding(
                    definition,
                    $"{definition.Code}/scoring",
                    $"{definition.DisplayName}确定性计分"),
                ScoringImplementation = ApplicationSemantics.From(sourceApplication),
                Responses = definition.Items
                    .Where(item => run.Evaluation.ApplicableItemCodes.Contains(item.Code))
                    .Where(item => run.Answers.ContainsKey(item.Code))
                    .Select(item => ResponseSemantics.FromRun(definition, item, run.Answers[item.Code]))
                    .ToArray(),
                DerivedResults = run.Evaluation.Metrics.Select(metric => new DerivedResultSemantics(
                    ExpectedCoding(
                        definition,
                        $"{definition.Code}/result/{metric.Code}",
                        metric.Display),
                    metric.Value)).ToArray(),
                TotalScore = run.Evaluation.TotalScore,
                TotalScoreAbsentReason = run.Evaluation.TotalScore is null
                    ? run.Evaluation.IsComplete
                        ? DataAbsentReasonCode.NotApplicable
                        : DataAbsentReasonCode.NotEstablished
                    : null,
                Interpretation = run.Evaluation.Interpretation is { } interpretation
                    ? ExpectedCoding(
                        definition,
                        $"{definition.Code}/interpretation/{interpretation.Code}",
                        interpretation.Display)
                    : null,
                Performer = run.Performer is null
                    ? null
                    : new PerformerSemantics(
                        "https://eznutrition.cdorey.net/identifiers/users",
                        run.Performer.UserId,
                        run.Performer.RealName ?? run.Performer.UserName,
                        run.Performer.InstitutionName)
            };
        }

        public static AssessmentSemantics FromResource(NutritionScaleAssessmentResource resource)
        {
            var scoringMethod = Assert.IsType<AlgorithmIdentity>(resource.ScoringMethod);
            var scoringImplementation = Assert.IsType<ApplicationIdentity>(scoringMethod.Implementation);
            return new AssessmentSemantics
            {
                ResourceId = resource.Metadata.ResourceId.Value,
                ResourceVersionId = resource.Metadata.VersionId.Value,
                EffectiveAt = resource.EffectiveAt,
                Instrument = CodingSemantics.From(resource.Instrument.Code),
                InstrumentVersion = Assert.IsType<string>(resource.Instrument.Version),
                DefinitionUri = Assert.IsType<CanonicalReference>(resource.Instrument.Definition)
                    .Uri.AbsoluteUri,
                DefinitionVersion = Assert.IsType<string>(resource.Instrument.Definition!.Version),
                ScoringMethod = CodingSemantics.From(scoringMethod.Method),
                ScoringImplementation = ApplicationSemantics.From(scoringImplementation),
                Responses = resource.Responses.Select(ResponseSemantics.FromResource).ToArray(),
                DerivedResults = resource.DerivedResults.Select(result => new DerivedResultSemantics(
                    CodingSemantics.From(result.Name),
                    Assert.IsType<DecimalArchiveValue>(result.Value).Value)).ToArray(),
                TotalScore = resource.TotalScore,
                TotalScoreAbsentReason = resource.TotalScoreAbsentReason,
                Interpretation = resource.Interpretation is null
                    ? null
                    : CodingSemantics.From(resource.Interpretation),
                Performer = resource.Performer is null
                    ? null
                    : new PerformerSemantics(
                        Assert.IsType<BusinessIdentifier>(resource.Performer.Identifier)
                            .System.AbsoluteUri,
                        resource.Performer.Identifier!.Value,
                        resource.Performer.Display,
                        resource.Performer.Organization?.Display)
            };
        }

        private static CodingSemantics ExpectedCoding(
            NutritionAssessmentDefinition definition,
            string code,
            string display) => new(
                definition.CodeSystem.AbsoluteUri,
                code,
                definition.Version,
                display);
    }

    private sealed record ResponseSemantics(
        CodingSemantics Item,
        AnswerSemantics Answer,
        decimal? ScoreContribution)
    {
        public static ResponseSemantics FromRun(
            NutritionAssessmentDefinition definition,
            NutritionAssessmentItem item,
            NutritionAssessmentAnswer answer) => new(
                new CodingSemantics(
                    definition.CodeSystem.AbsoluteUri,
                    $"{definition.Code}/item/{item.Code}",
                    definition.Version,
                    item.Prompt),
                AnswerSemantics.FromRun(definition, item, answer),
                CalculateScoreContribution(item, answer));

        public static ResponseSemantics FromResource(AssessmentItemResponse response) => new(
            CodingSemantics.From(response.Item),
            AnswerSemantics.FromResource(response.Answer),
            response.ScoreContribution);

        private static decimal? CalculateScoreContribution(
            NutritionAssessmentItem item,
            NutritionAssessmentAnswer answer) => answer switch
            {
                NutritionAssessmentSingleChoiceAnswer single => item.Options
                    .Single(option => option.Code == single.OptionCode).Score,
                NutritionAssessmentMultipleChoiceAnswer multiple => MultipleChoiceScore(
                    item,
                    multiple),
                _ => null
            };

        private static decimal? MultipleChoiceScore(
            NutritionAssessmentItem item,
            NutritionAssessmentMultipleChoiceAnswer answer)
        {
            var selected = item.Options
                .Where(option => answer.OptionCodes.Contains(option.Code, StringComparer.Ordinal))
                .ToArray();
            return selected.Any(option => option.Score is null)
                ? null
                : selected.Sum(option => option.Score!.Value);
        }
    }

    private sealed record AnswerSemantics(
        ArchiveValueKind? Kind,
        IReadOnlyList<CodingSemantics> Codings,
        decimal? DecimalValue)
    {
        public static AnswerSemantics FromRun(
            NutritionAssessmentDefinition definition,
            NutritionAssessmentItem item,
            NutritionAssessmentAnswer answer) => answer switch
            {
                NutritionAssessmentSingleChoiceAnswer single => new AnswerSemantics(
                    ArchiveValueKind.Coding,
                    [ExpectedOptionCoding(definition, item, single.OptionCode)],
                    null),
                NutritionAssessmentMultipleChoiceAnswer multiple => new AnswerSemantics(
                    ArchiveValueKind.CodingCollection,
                    item.Options
                        .Where(option => multiple.OptionCodes.Contains(
                            option.Code,
                            StringComparer.Ordinal))
                        .Select(option => ExpectedOptionCoding(definition, item, option.Code))
                        .ToArray(),
                    null),
                NutritionAssessmentDecimalAnswer number => new AnswerSemantics(
                    ArchiveValueKind.Decimal,
                    [],
                    number.Value),
                _ => throw new InvalidOperationException("运行态量表包含未知回答类型。")
            };

        public static AnswerSemantics FromResource(ArchiveValue? answer) => answer switch
        {
            null => new AnswerSemantics(null, [], null),
            CodingArchiveValue coding => new AnswerSemantics(
                coding.Kind,
                [CodingSemantics.From(coding.Value)],
                null),
            CodingCollectionArchiveValue collection => new AnswerSemantics(
                collection.Kind,
                collection.Values.Select(CodingSemantics.From).ToArray(),
                null),
            DecimalArchiveValue number => new AnswerSemantics(number.Kind, [], number.Value),
            _ => throw new InvalidOperationException(
                $"档案量表包含不支持的回答值类型 {answer.Kind}。")
        };

        private static CodingSemantics ExpectedOptionCoding(
            NutritionAssessmentDefinition definition,
            NutritionAssessmentItem item,
            string optionCode)
        {
            var option = item.Options.Single(candidate => candidate.Code == optionCode);
            return new CodingSemantics(
                definition.CodeSystem.AbsoluteUri,
                $"{definition.Code}/item/{item.Code}/answer/{option.Code}",
                definition.Version,
                option.Display);
        }
    }

    private sealed record DerivedResultSemantics(CodingSemantics Name, decimal Value);

    private sealed record CodingSemantics(
        string System,
        string Code,
        string? Version,
        string? Display)
    {
        public static CodingSemantics From(Coding coding) => new(
            coding.System.AbsoluteUri,
            coding.Code,
            coding.Version,
            coding.Display);
    }

    private sealed record ApplicationSemantics(string Identifier, string Name, string Version)
    {
        public static ApplicationSemantics From(ApplicationIdentity application) => new(
            application.Identifier.AbsoluteUri,
            application.Name,
            application.Version);
    }

    private sealed record PerformerSemantics(
        string IdentifierSystem,
        string IdentifierValue,
        string? Display,
        string? OrganizationDisplay);
}
