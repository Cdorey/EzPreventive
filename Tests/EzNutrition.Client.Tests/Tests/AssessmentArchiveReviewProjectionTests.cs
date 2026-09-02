using System.Globalization;
using EzNutrition.Assessments.Common;
using EzNutrition.Application.Archives;
using EzNutrition.Application.Consultations;
using EzNutrition.Archives.Contracts.Abstractions;
using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Archives.Contracts.Serialization;
using EzNutrition.Archives.Contracts.Validation;
using EzNutrition.Archives.Contracts.ValueObjects;
using EzNutrition.Archives.Xml;
using EzNutrition.Client.Tests.Fixtures;
using EzNutrition.Domain.Assessments;
using EzNutrition.Domain.Consultations;

namespace EzNutrition.Client.Tests.Tests;

/// <summary>
/// 验证档案浏览模型只依赖归档快照，并忠实投影各种量表回答形状。
/// </summary>
public sealed class AssessmentArchiveReviewProjectionTests
{
    private static readonly ApplicationIdentity SourceApplication = new(
        new Uri("https://example.invalid/applications/assessment-review-semantics"),
        "量表档案浏览测试",
        "2.1.0.0");

    private static readonly ArchiveContractAssembler Assembler = new(SourceApplication);
    private static readonly ArchiveContractValidator Validator = new();

    public static IEnumerable<object[]> ReviewCases()
    {
        yield return ["nrs-2002", "attention"];
        yield return ["must", "zero-score"];
        yield return ["mna-sf", "calf-circumference"];
        yield return ["sga", "score-not-applicable"];
        yield return ["sga-chas-2020", "derived-counts"];
        yield return ["pg-sga", "typed-and-conditional"];
        yield return ["ws-t-552-elderly-malnutrition-risk", "screening-only"];
        yield return ["ws-t-552-elderly-malnutrition-risk", "full-assessment"];
    }

    [Theory]
    [MemberData(nameof(ReviewCases))]
    public async Task Archived_scale_details_are_projected_without_an_instrument_registry(
        string instrumentCode,
        string variant)
    {
        var scenario = SelectScenario(instrumentCode, variant);
        var review = await SaveAndOpenAsync(scenario.Workspace);
        var section = Assert.Single(
            review.Sections,
            candidate => candidate.Title == scenario.Run.Definition.DisplayName);

        AssertReviewSemantics(scenario.Run, section);
    }

    [Fact]
    public async Task Archived_wording_and_version_remain_visible_after_the_runtime_definition_changes()
    {
        var scenario = AssessmentArchiveScenarioCatalog.Create("nrs-2002")[0];
        var document = CreateDocument(scenario);
        var archivedScale = Assert.Single(
            document.Bundle.Entries.OfType<NutritionScaleAssessmentResource>());
        var firstResponse = archivedScale.Responses[0];
        var firstAnswer = Assert.IsType<CodingArchiveValue>(firstResponse.Answer);
        var legacyScale = archivedScale with
        {
            Instrument = archivedScale.Instrument with
            {
                Code = CopyCoding(
                    archivedScale.Instrument.Code,
                    version: "legacy-code-system-version",
                    display: "归档时使用的量表名称"),
                Version = "归档临床版本",
                Definition = new CanonicalReference(
                    new Uri("https://example.invalid/archived-scale-definition"),
                    "归档临床版本")
            },
            Responses =
            [
                firstResponse with
                {
                    Item = CopyCoding(firstResponse.Item, display: "归档时的题目文字"),
                    Answer = new CodingArchiveValue(CopyCoding(
                        firstAnswer.Value,
                        display: "归档时的选项文字"))
                },
                .. archivedScale.Responses.Skip(1)
            ],
            Interpretation = CopyCoding(
                Assert.IsType<Coding>(archivedScale.Interpretation),
                display: "归档时的结果解释")
        };
        var modifiedDocument = document with
        {
            Bundle = document.Bundle with
            {
                Entries = document.Bundle.Entries
                    .Select<IArchiveResource, IArchiveResource>(resource =>
                        ReferenceEquals(resource, archivedScale) ? legacyScale : resource)
                    .ToArray()
            }
        };

        var review = await ImportAsync(modifiedDocument);
        var section = Assert.Single(
            review.Sections,
            candidate => candidate.Title == "归档时使用的量表名称");
        var responses = Assert.Single(
            section.DetailGroups,
            group => group.Title == "逐题作答");

        Assert.Equal("归档临床版本", TextField(section.Fields, "量表版本"));
        Assert.Equal("归档时的结果解释", section.Description);
        Assert.Equal(
            "归档时的选项文字（本题 0 分）",
            TextField(responses.Fields, "归档时的题目文字"));
    }

    [Fact]
    public async Task Missing_performer_is_shown_as_missing_without_blocking_archive_review()
    {
        var createdAt = new DateTimeOffset(2026, 8, 2, 9, 0, 0, TimeSpan.FromHours(8));
        var workspace = new ConsultationWorkspace(
            new ClientInfo
            {
                Name = "无调查人快照的合成对象",
                Gender = "女",
                Age = new EzNutrition.Domain.Consultations.ChronologicalAge(30),
                Height = 165m,
                Weight = 55m
            },
            ArchiveContractIdentity.Create(createdAt));
        var service = new NutritionAssessmentApplicationService([new MustInstrument()]);
        var run = service.StartRun(
            workspace,
            Assert.Single(service.Definitions),
            createdAt);
        foreach (var item in run.Definition.Items)
        {
            run.SetAnswer(item.Code, item.Options[0].Code, createdAt.AddMinutes(1));
        }

        var review = await SaveAndOpenAsync(workspace);
        var section = Assert.Single(
            review.Sections,
            candidate => candidate.Title == run.Definition.DisplayName);
        var record = Assert.Single(
            section.DetailGroups,
            group => group.Title == "记录信息");

        Assert.Equal("未提供", TextField(record.Fields, "调查人"));
        Assert.DoesNotContain(record.Fields, field => field.Label == "调查机构");
    }

    [Fact]
    public async Task Incomplete_scale_draft_is_distinguished_from_a_complete_scoreless_scale()
    {
        var scenario = AssessmentArchiveScenarioCatalog.Create("sga")[0];
        var answerToRemove = scenario.Run.Definition.Items.Last(item =>
            scenario.Run.Answers.ContainsKey(item.Code));
        scenario.Run.ClearAnswer(
            answerToRemove.Code,
            scenario.Run.LastModifiedAt.AddMinutes(1));

        var review = await SaveAndOpenAsync(scenario.Workspace);
        var section = Assert.Single(
            review.Sections,
            candidate => candidate.Title == scenario.Run.Definition.DisplayName);
        var conclusion = Assert.Single(
            section.DetailGroups,
            group => group.Title == "评估结论");

        Assert.Equal("尚未建立或无法取得", TextField(conclusion.Fields, "总分"));
        Assert.Equal("尚未形成", TextField(conclusion.Fields, "结果解释"));
    }

    private static AssessmentArchiveScenario SelectScenario(
        string instrumentCode,
        string variant)
    {
        var scenarios = AssessmentArchiveScenarioCatalog.Create(instrumentCode);
        return variant switch
        {
            "attention" => scenarios.First(scenario =>
                scenario.Run.Evaluation.Interpretation?.AttentionLevel
                    == NutritionAssessmentAttentionLevel.RequiresAttention),
            "zero-score" => scenarios.First(scenario => scenario.Run.Evaluation.TotalScore == 0m),
            "calf-circumference" => scenarios.First(scenario =>
                scenario.Run.Subject.BodyMassIndex is null),
            "score-not-applicable" => scenarios.First(scenario =>
                scenario.Run.Evaluation.TotalScore is null),
            "derived-counts" => scenarios.First(scenario =>
                scenario.Run.Evaluation.Metrics.Count > 0),
            "typed-and-conditional" => scenarios.First(scenario =>
                scenario.Run.Subject.WeightInKilograms is null
                && scenario.Run.Answers.ContainsKey("reference-weight")
                && scenario.Run.Answers.ContainsKey("fever-duration")
                && scenario.Run.Answers.Values.OfType<NutritionAssessmentMultipleChoiceAnswer>()
                    .Any(answer => answer.OptionCodes.Count > 1)),
            "screening-only" => scenarios.First(scenario =>
                scenario.Run.Evaluation.ApplicableItemCodes.Count == 6),
            "full-assessment" => scenarios.First(scenario =>
                scenario.Run.Evaluation.ApplicableItemCodes.Count == 20),
            _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, null)
        };
    }

    private static ArchiveDocument CreateDocument(AssessmentArchiveScenario scenario) =>
        Assembler.CreateDocument(
            scenario.Workspace,
            scenario.Run.LastModifiedAt.AddMinutes(1));

    private static async Task<ArchiveReview> SaveAndOpenAsync(
        ConsultationWorkspace workspace)
    {
        var store = new MemoryStore();
        var workflow = new ArchiveWorkflow(
            Assembler,
            Validator,
            [new XmlArchiveCodec(Validator)],
            store,
            new DisabledTransport());

        var save = await workflow.SaveCurrentAsync(workspace);
        Assert.True(save.IsSuccess, save.Message);
        var record = Assert.Single((await workflow.BrowseAsync()).Records);
        var stored = Assert.Single(store.Documents.Values);
        Assert.False(stored.Content.IsEmpty);

        var opened = await workflow.OpenStoredAsync(record.DocumentId);
        Assert.True(opened.Operation.IsSuccess, opened.Operation.Message);
        return Assert.IsType<ArchiveReview>(opened.Review);
    }

    private static async Task<ArchiveReview> ImportAsync(ArchiveDocument document)
    {
        var codec = new XmlArchiveCodec(Validator);
        await using var stream = new MemoryStream();
        var write = await codec.WriteAsync(new ArchiveWriteRequest
        {
            Document = document,
            TargetFormat = XmlArchiveFormat.Current
        }, stream);
        Assert.True(
            write.IsSuccess,
            string.Join(" | ", write.Validation.Issues.Select(issue => issue.Code)));

        var transport = new ImportTransport(stream.ToArray());
        var workflow = new ArchiveWorkflow(
            Assembler,
            Validator,
            [new XmlArchiveCodec(Validator)],
            new EmptyStore(),
            transport);
        var opened = await workflow.ImportAsync();

        Assert.True(opened.Operation.IsSuccess, opened.Operation.Message);
        return Assert.IsType<ArchiveReview>(opened.Review);
    }

    private static void AssertReviewSemantics(
        NutritionAssessmentRun run,
        ArchiveReviewSection section)
    {
        var answeredItems = run.Definition.Items
            .Where(item => run.Evaluation.ApplicableItemCodes.Contains(item.Code))
            .Where(item => run.Answers.ContainsKey(item.Code))
            .ToArray();
        Assert.Equal(
            run.Evaluation.Interpretation?.Display ?? "量表尚未形成完整解释",
            section.Description);
        Assert.Equal(run.Definition.Version, TextField(section.Fields, "量表版本"));
        Assert.Equal(
            answeredItems.Length.ToString(CultureInfo.InvariantCulture),
            TextField(section.Fields, "已回答题目"));
        Assert.Equal(
            run.Evaluation.TotalScore?.ToString(CultureInfo.InvariantCulture)
                ?? "不适用或尚未形成",
            TextField(section.Fields, "总分"));

        var record = Assert.Single(
            section.DetailGroups,
            group => group.Title == "记录信息");
        Assert.Equal(run.Definition.Code, TextField(record.Fields, "量表编码"));
        Assert.Equal(run.Definition.Version, TextField(record.Fields, "量表版本"));
        Assert.Equal(run.Performer?.RealName, TextField(record.Fields, "调查人"));
        Assert.Equal(
            run.Performer?.InstitutionName,
            TextField(record.Fields, "调查机构"));
        Assert.Equal(
            $"{run.Definition.DefinitionUri.AbsoluteUri} · 版本 {run.Definition.Version}",
            TextField(record.Fields, "定义来源"));
        Assert.Equal(
            $"{run.Definition.DisplayName}确定性计分",
            TextField(record.Fields, "计分方法"));
        Assert.Equal(
            $"{SourceApplication.Name} {SourceApplication.Version}",
            TextField(record.Fields, "计分实现"));

        var responseGroup = Assert.Single(
            section.DetailGroups,
            group => group.Title == "逐题作答");
        Assert.Equal(answeredItems.Length, responseGroup.Fields.Count);
        foreach (var item in answeredItems)
        {
            Assert.Equal(
                FormatAnswer(run, item),
                TextField(responseGroup.Fields, item.Prompt));
        }

        if (run.Evaluation.Metrics.Count > 0)
        {
            var derivedGroup = Assert.Single(
                section.DetailGroups,
                group => group.Title == "派生结果");
            Assert.Equal(run.Evaluation.Metrics.Count, derivedGroup.Fields.Count);
            foreach (var metric in run.Evaluation.Metrics)
            {
                Assert.Equal(
                    FormatDecimal(metric.Value),
                    TextField(derivedGroup.Fields, metric.Display));
            }
        }

        var conclusion = Assert.Single(
            section.DetailGroups,
            group => group.Title == "评估结论");
        Assert.Equal(
            run.Evaluation.TotalScore is { } total
                ? $"{FormatDecimal(total)} 分"
                : run.Evaluation.IsComplete
                    ? "不适用"
                    : "尚未建立或无法取得",
            TextField(conclusion.Fields, "总分"));
        Assert.Equal(
            run.Evaluation.Interpretation?.Display ?? "尚未形成",
            TextField(conclusion.Fields, "结果解释"));
    }

    private static string FormatAnswer(
        NutritionAssessmentRun run,
        NutritionAssessmentItem item)
    {
        var answer = run.Answers[item.Code];
        var display = answer switch
        {
            NutritionAssessmentSingleChoiceAnswer single => item.Options
                .Single(option => option.Code == single.OptionCode).Display,
            NutritionAssessmentMultipleChoiceAnswer multiple => string.Join(
                "、",
                item.Options
                    .Where(option => multiple.OptionCodes.Contains(
                        option.Code,
                        StringComparer.Ordinal))
                    .Select(option => option.Display)),
            NutritionAssessmentDecimalAnswer number => FormatDecimal(number.Value),
            _ => throw new InvalidOperationException("测试包含未知量表回答类型。")
        };
        var score = ScoreContribution(item, answer);
        return score is { } value
            ? $"{display}（本题 {FormatDecimal(value)} 分）"
            : display;
    }

    private static decimal? ScoreContribution(
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

    private static string TextField(
        IEnumerable<ArchiveReviewField> fields,
        string label) => Assert.IsType<string>(
            Assert.Single(fields, field => field.Label == label).Value);

    private static string FormatDecimal(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);

    private static Coding CopyCoding(
        Coding source,
        string? version = null,
        string? display = null) => new(
            source.System,
            source.Code,
            version ?? source.Version,
            display ?? source.Display);

    private sealed class EmptyStore : IArchiveDocumentStore
    {
        public ArchiveDocumentStoreCapabilities Capabilities =>
            ArchiveDocumentStoreCapabilities.None;

        public ValueTask SaveAsync(
            StoredArchiveDocument document,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<IReadOnlyList<StoredArchiveDocumentInfo>> ListAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<StoredArchiveDocumentInfo>>([]);

        public ValueTask<StoredArchiveDocument?> GetAsync(
            Guid documentId,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<StoredArchiveDocument?>(null);

        public ValueTask DeleteAsync(
            Guid documentId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask ClearAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class MemoryStore : IArchiveDocumentStore
    {
        public Dictionary<Guid, StoredArchiveDocument> Documents { get; } = [];

        public ArchiveDocumentStoreCapabilities Capabilities =>
            ArchiveDocumentStoreCapabilities.Save |
            ArchiveDocumentStoreCapabilities.Browse;

        public ValueTask SaveAsync(
            StoredArchiveDocument document,
            CancellationToken cancellationToken = default)
        {
            Documents[document.Info.DocumentId] = document;
            return ValueTask.CompletedTask;
        }

        public ValueTask<IReadOnlyList<StoredArchiveDocumentInfo>> ListAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<StoredArchiveDocumentInfo>>(
                Documents.Values.Select(document => document.Info).ToArray());

        public ValueTask<StoredArchiveDocument?> GetAsync(
            Guid documentId,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Documents.GetValueOrDefault(documentId));

        public ValueTask DeleteAsync(
            Guid documentId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask ClearAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class DisabledTransport : IArchiveDocumentTransport
    {
        public bool CanOpen => false;

        public bool CanSave => false;

        public ValueTask<ExternalArchiveDocument?> OpenAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<bool> SaveAsync(
            ArchiveDocumentExport document,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class ImportTransport(ReadOnlyMemory<byte> content) : IArchiveDocumentTransport
    {
        public bool CanOpen => true;

        public bool CanSave => false;

        public ValueTask<ExternalArchiveDocument?> OpenAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<ExternalArchiveDocument?>(new ExternalArchiveDocument
            {
                FileName = "assessment.archive.xml",
                MediaType = XmlArchiveFormat.MediaType,
                Content = content
            });

        public ValueTask<bool> SaveAsync(
            ArchiveDocumentExport document,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
