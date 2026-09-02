using System.Globalization;
using EzNutrition.Assessments.Common;
using EzNutrition.Application.Archives;
using EzNutrition.Application.Consultations;
using EzNutrition.Domain.Assessments;
using EzNutrition.Domain.Consultations;
using EzNutrition.Shared.Identities;

namespace EzNutrition.Client.Tests.Fixtures;

/// <summary>
/// 表示一个可以进入真实档案管线的确定性量表作答场景。
/// </summary>
internal sealed record AssessmentArchiveScenario(
    string Id,
    ConsultationWorkspace Workspace,
    NutritionAssessmentRun Run);

/// <summary>
/// 为档案语义保真测试提供小量表穷举和复杂量表分支化场景。
/// </summary>
internal static class AssessmentArchiveScenarioCatalog
{
    private static readonly DateTimeOffset BaseTime =
        new(2026, 8, 1, 9, 0, 0, TimeSpan.FromHours(8));

    private static readonly TestUser Performer = new();

    private static readonly IReadOnlyDictionary<string, int> TargetCounts =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["nrs-2002"] = 384,
            ["must"] = 18,
            ["mna-sf"] = 120,
            ["sga"] = 100,
            ["sga-chas-2020"] = 100,
            ["pg-sga"] = 200,
            ["ws-t-552-elderly-malnutrition-risk"] = 200
        };

    public static IReadOnlyList<string> InstrumentCodes { get; } =
        TargetCounts.Keys.ToArray();

    public static int TargetCount(string instrumentCode) =>
        TargetCounts.TryGetValue(instrumentCode, out var count)
            ? count
            : throw new ArgumentOutOfRangeException(
                nameof(instrumentCode),
                instrumentCode,
                "没有为指定量表定义档案测试预算。");

    public static IReadOnlyList<AssessmentArchiveScenario> Create(string instrumentCode)
    {
        var instrument = CreateInstrument(instrumentCode);
        return instrumentCode switch
        {
            "nrs-2002" => CreateCartesianScenarios(
                instrument,
                [new ScenarioSubject(69, 170m, 65m), new ScenarioSubject(70, 170m, 65m)]),
            "must" => CreateCartesianScenarios(
                instrument,
                [new ScenarioSubject(50, 170m, 65m)]),
            _ => CreateGeneratedScenarios(instrument, TargetCount(instrumentCode))
        };
    }

    private static INutritionAssessmentInstrument CreateInstrument(string instrumentCode) =>
        instrumentCode switch
        {
            "nrs-2002" => new Nrs2002Instrument(),
            "must" => new MustInstrument(),
            "mna-sf" => new MnaSfInstrument(),
            "sga" => new SgaInstrument(),
            "sga-chas-2020" => new ChasSgaInstrument(),
            "pg-sga" => new PgSgaInstrument(),
            "ws-t-552-elderly-malnutrition-risk" =>
                new WsT552ElderlyMalnutritionRiskInstrument(),
            _ => throw new ArgumentOutOfRangeException(
                nameof(instrumentCode),
                instrumentCode,
                "没有找到指定的常用量表实现。")
        };

    private static IReadOnlyList<AssessmentArchiveScenario> CreateCartesianScenarios(
        INutritionAssessmentInstrument instrument,
        IReadOnlyList<ScenarioSubject> subjects)
    {
        var items = instrument.Definition.Items.ToArray();
        if (items.Any(item => item.ResponseType != NutritionAssessmentResponseType.SingleChoice))
        {
            throw new InvalidOperationException("笛卡尔积场景只适用于全部由单选题组成的量表。");
        }

        var combinationsPerSubject = items.Aggregate(
            1,
            (product, item) => checked(product * item.Options.Count));
        var result = new List<AssessmentArchiveScenario>(
            checked(combinationsPerSubject * subjects.Count));
        var sequence = 0;
        foreach (var subject in subjects)
        {
            for (var combination = 0; combination < combinationsPerSubject; combination++)
            {
                var scenario = StartScenario(instrument, subject, sequence);
                var remainder = combination;
                foreach (var item in items)
                {
                    var optionIndex = remainder % item.Options.Count;
                    remainder /= item.Options.Count;
                    scenario.Run.SetAnswer(
                        item.Code,
                        item.Options[optionIndex].Code,
                        scenario.Run.CreatedAt.AddMinutes(1));
                }

                result.Add(scenario with
                {
                    Id = $"{instrument.Definition.Code}-combination-{sequence:D4}"
                });
                sequence++;
            }
        }

        return result;
    }

    private static IReadOnlyList<AssessmentArchiveScenario> CreateGeneratedScenarios(
        INutritionAssessmentInstrument instrument,
        int targetCount)
    {
        var result = new List<AssessmentArchiveScenario>(targetCount);
        var fingerprints = new HashSet<string>(StringComparer.Ordinal);
        for (var seed = 0; result.Count < targetCount && seed < 100_000; seed++)
        {
            var scenario = StartScenario(
                instrument,
                SubjectFor(instrument.Definition.Code, seed),
                seed);
            CompleteGeneratedScenario(scenario.Run, seed);
            var fingerprint = Fingerprint(scenario.Run);
            if (!fingerprints.Add(fingerprint))
            {
                continue;
            }

            result.Add(scenario with
            {
                Id = $"{instrument.Definition.Code}-seed-{seed:D5}"
            });
        }

        if (result.Count != targetCount)
        {
            throw new InvalidOperationException(
                $"量表 {instrument.Definition.Code} 只生成了 {result.Count} 个互异场景，目标为 {targetCount} 个。");
        }

        return result;
    }

    private static AssessmentArchiveScenario StartScenario(
        INutritionAssessmentInstrument instrument,
        ScenarioSubject subject,
        int sequence)
    {
        var client = new ClientInfo
        {
            Name = $"量表档案合成对象 {instrument.Definition.Code} {sequence:D5}",
            Gender = subject.Gender,
            Age = new ChronologicalAge(subject.AgeInYears),
            Height = subject.HeightInCentimeters,
            Weight = subject.WeightInKilograms
        };
        var createdAt = BaseTime.AddSeconds(sequence);
        var workspace = new ConsultationWorkspace(
            client,
            ArchiveContractIdentity.Create(createdAt));
        var service = new NutritionAssessmentApplicationService([instrument]);
        var run = service.StartRun(
            workspace,
            instrument.Definition,
            createdAt,
            Performer);
        return new AssessmentArchiveScenario(
            $"{instrument.Definition.Code}-{sequence:D5}",
            workspace,
            run);
    }

    private static ScenarioSubject SubjectFor(string instrumentCode, int seed) =>
        instrumentCode switch
        {
            "mna-sf" => MnaSubject(seed),
            "pg-sga" => PgSgaSubject(seed),
            "ws-t-552-elderly-malnutrition-risk" => new ScenarioSubject(
                seed % 2 == 0 ? 69 : 70,
                165m,
                60m,
                seed % 2 == 0 ? "女" : "男"),
            _ => new ScenarioSubject(
                seed % 2 == 0 ? 49 : 70,
                165m,
                60m,
                seed % 2 == 0 ? "女" : "男")
        };

    private static ScenarioSubject MnaSubject(int seed)
    {
        if (seed % 2 != 0)
        {
            return new ScenarioSubject(75, null, null);
        }

        var representativeBmis = new[] { 18.5m, 20m, 22m, 24m };
        var bmi = representativeBmis[(seed / 2) % representativeBmis.Length];
        return new ScenarioSubject(75, 100m, bmi);
    }

    private static ScenarioSubject PgSgaSubject(int seed)
    {
        var weights = new[] { 50m, 60m, 70m, 80m };
        return new ScenarioSubject(
            seed % 2 == 0 ? 65 : 66,
            165m,
            seed % 4 == 0 ? null : weights[seed % weights.Length],
            seed % 2 == 0 ? "女" : "男");
    }

    private static void CompleteGeneratedScenario(NutritionAssessmentRun run, int seed)
    {
        var definitionItems = run.Definition.Items.ToArray();
        while (!run.Evaluation.IsComplete)
        {
            var nextItem = definitionItems.FirstOrDefault(item =>
                run.Evaluation.ApplicableItemCodes.Contains(item.Code)
                && !run.Answers.ContainsKey(item.Code));
            if (nextItem is null)
            {
                throw new InvalidOperationException(
                    $"量表 {run.Definition.Code} 尚未完成，但没有可回答的适用题目。");
            }

            var ordinal = Array.IndexOf(definitionItems, nextItem);
            SetGeneratedAnswer(run, nextItem, ordinal, seed);
        }
    }

    private static void SetGeneratedAnswer(
        NutritionAssessmentRun run,
        NutritionAssessmentItem item,
        int ordinal,
        int seed)
    {
        var modifiedAt = run.CreatedAt.AddMinutes(1);
        switch (item.ResponseType)
        {
            case NutritionAssessmentResponseType.SingleChoice:
                var optionIndex = SingleChoiceIndex(run, item, ordinal, seed);
                run.SetAnswer(item.Code, item.Options[optionIndex].Code, modifiedAt);
                break;
            case NutritionAssessmentResponseType.MultipleChoice:
                run.SetMultipleChoiceAnswer(
                    item.Code,
                    MultipleChoiceCodes(run, item, ordinal, seed),
                    modifiedAt);
                break;
            case NutritionAssessmentResponseType.Decimal:
                run.SetDecimalAnswer(
                    item.Code,
                    DecimalValue(run, item, ordinal, seed),
                    modifiedAt);
                break;
            default:
                throw new InvalidOperationException(
                    $"测试场景生成器尚不支持题型 {item.ResponseType}。");
        }
    }

    private static int SingleChoiceIndex(
        NutritionAssessmentRun run,
        NutritionAssessmentItem item,
        int ordinal,
        int seed)
    {
        if (run.Definition.Code == "pg-sga")
        {
            var lowScoreVariant = seed % 20;
            if (item.Code == "weight-reference")
            {
                return lowScoreVariant is 0 or 1 ? 0 : seed % item.Options.Count;
            }

            if (lowScoreVariant is 0 or 1)
            {
                if (lowScoreVariant == 1 && item.Code == "two-week-weight-trend")
                {
                    return IndexOfScore(item, 1m);
                }

                return IndexOfScore(item, item.Options.Min(option => option.Score)!.Value);
            }

            if (item.Code == "fever")
            {
                return (seed / 3) % item.Options.Count;
            }
        }

        if (run.Definition.Code == "ws-t-552-elderly-malnutrition-risk"
            && seed % 11 == 2)
        {
            var targetScore = ordinal == 0
                ? item.Options.Min(option => option.Score)!.Value
                : item.Options.Max(option => option.Score)!.Value;
            return IndexOfScore(item, targetScore);
        }

        if (run.Definition.Code == "ws-t-552-elderly-malnutrition-risk"
            && ordinal < 6)
        {
            if (seed % 7 == 0)
            {
                return IndexOfScore(item, item.Options.Max(option => option.Score)!.Value);
            }

            if (seed % 7 == 1)
            {
                return IndexOfScore(item, item.Options.Min(option => option.Score)!.Value);
            }
        }

        return StableSelection(seed, ordinal, item.Options.Count);
    }

    private static int IndexOfScore(NutritionAssessmentItem item, decimal score)
    {
        for (var index = 0; index < item.Options.Count; index++)
        {
            if (item.Options[index].Score == score)
            {
                return index;
            }
        }

        throw new InvalidOperationException($"题目 {item.Code} 没有分值为 {score} 的选项。");
    }

    private static IReadOnlyList<string> MultipleChoiceCodes(
        NutritionAssessmentRun run,
        NutritionAssessmentItem item,
        int ordinal,
        int seed)
    {
        var exclusive = item.Options.Where(option => option.IsExclusive).ToArray();
        if (run.Definition.Code == "pg-sga"
            && seed % 20 is 0 or 1
            && exclusive.Length > 0)
        {
            return [exclusive[0].Code];
        }

        if (exclusive.Length > 0 && StableSelection(seed, ordinal + 29, 5) == 0)
        {
            return [exclusive[StableSelection(seed, ordinal + 31, exclusive.Length)].Code];
        }

        var selectable = item.Options.Where(option => !option.IsExclusive).ToArray();
        var selected = selectable
            .Where((_, index) => StableSelection(seed, ordinal * 17 + index + 1, 3) == 0)
            .Select(option => option.Code)
            .ToArray();
        return selected.Length > 0
            ? selected
            : [selectable[StableSelection(seed, ordinal + 37, selectable.Length)].Code];
    }

    private static decimal DecimalValue(
        NutritionAssessmentRun run,
        NutritionAssessmentItem item,
        int ordinal,
        int seed)
    {
        if (run.Definition.Code == "pg-sga")
        {
            if (item.Code == "current-weight")
            {
                var weights = new[] { 45m, 50m, 60m, 70m, 80m };
                return weights[StableSelection(seed, ordinal, weights.Length)];
            }

            if (item.Code == "reference-weight")
            {
                var currentWeight = run.Subject.WeightInKilograms
                    ?? run.GetDecimalAnswer("current-weight")
                    ?? throw new InvalidOperationException("PG-SGA 场景缺少当前体重。");
                var referenceKind = run.GetAnswer("weight-reference");
                if (seed % 20 is 0 or 1)
                {
                    return currentWeight;
                }

                var percentages = referenceKind == "one-month"
                    ? new[] { 0m, 1.9m, 2m, 2.9m, 3m, 4.9m, 5m, 9.9m, 10m, 15m }
                    : new[] { 0m, 1.9m, 2m, 5.9m, 6m, 9.9m, 10m, 19.9m, 20m, 25m };
                var percentage = percentages[StableSelection(seed, ordinal, percentages.Length)];
                return decimal.Round(currentWeight / (1m - percentage / 100m), 6);
            }
        }

        var minimum = item.MinimumValue ?? 0m;
        var maximum = item.MaximumValue ?? minimum + 100m;
        var candidates = new[] { minimum, (minimum + maximum) / 2m, maximum };
        return candidates[StableSelection(seed, ordinal, candidates.Length)];
    }

    private static int StableSelection(int seed, int ordinal, int count)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        var value = unchecked((uint)(seed + 1) * 0x9E3779B9u
            + (uint)(ordinal + 1) * 0x85EBCA6Bu);
        value ^= value >> 16;
        value *= 0x7FEB352Du;
        value ^= value >> 15;
        value *= 0x846CA68Bu;
        value ^= value >> 16;
        return (int)(value % (uint)count);
    }

    private static string Fingerprint(NutritionAssessmentRun run)
    {
        var answerParts = run.Definition.Items
            .Where(item => run.Answers.ContainsKey(item.Code))
            .Select(item => $"{item.Code}={AnswerFingerprint(run.Answers[item.Code])}");
        return FormattableString.Invariant(
            $"{run.Subject.AgeInYears}|{run.Subject.HeightInCentimeters}|{run.Subject.WeightInKilograms}|{string.Join(";", answerParts)}");
    }

    private static string AnswerFingerprint(NutritionAssessmentAnswer answer) => answer switch
    {
        NutritionAssessmentSingleChoiceAnswer single => $"s:{single.OptionCode}",
        NutritionAssessmentMultipleChoiceAnswer multiple =>
            $"m:{string.Join(",", multiple.OptionCodes)}",
        NutritionAssessmentDecimalAnswer number =>
            $"d:{number.Value.ToString(CultureInfo.InvariantCulture)}",
        _ => throw new InvalidOperationException("场景包含未知回答类型。")
    };

    private sealed record ScenarioSubject(
        int AgeInYears,
        decimal? HeightInCentimeters,
        decimal? WeightInKilograms,
        string Gender = "女");

    private sealed class TestUser : IUserInfo
    {
        public string UserId => "archive-semantic-test-user";

        public string UserName => "archive-doctor";

        public string[] Roles => ["Doctor"];

        public string Email => "archive-doctor@example.invalid";

        public string? RealName => "档案语义测试医生";

        public string? InstitutionName => "合成临床营养中心";
    }
}
