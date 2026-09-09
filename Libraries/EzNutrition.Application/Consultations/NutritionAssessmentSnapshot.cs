using EzNutrition.Archives.Contracts.ValueObjects;
using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Domain.Assessments;

namespace EzNutrition.Application.Consultations;

/// <summary>捕获量表的业务结果，供档案组装及独立评估输出共用；不创建患者、咨询或报告资源。</summary>
public sealed record NutritionAssessmentSnapshot
{
    /// <summary>获取量表的确切身份和定义版本。</summary>
    public required AssessmentInstrumentIdentity Instrument { get; init; }
    /// <summary>获取本次作答路径下的题目与答案。</summary>
    public required IReadOnlyList<AssessmentItemResponse> Responses { get; init; }
    /// <summary>获取已有的分项计算结果。</summary>
    public required IReadOnlyList<NamedArchiveValue> DerivedResults { get; init; }
    /// <summary>获取已有总分，不在输出时重新计算。</summary>
    public decimal? TotalScore { get; init; }
    /// <summary>获取没有总分的原因，区分未完成和不适用。</summary>
    public DataAbsentReasonCode? TotalScoreAbsentReason { get; init; }
    /// <summary>获取已有的编码化解释。</summary>
    public Coding? Interpretation { get; init; }
    /// <summary>获取开始评估时使用的对象资料。</summary>
    public required NutritionAssessmentSubject Subject { get; init; }
    /// <summary>获取本次结果对应的完成或最后修改时间。</summary>
    public required DateTimeOffset EffectiveAt { get; init; }

    /// <summary>复制当前答案和结果；后续修改运行实例不会改变该快照。</summary>
    /// <param name="run">当前量表运行实例。</param>
    /// <param name="includeUnanswered">是否保留适用但尚未回答的题目；评估打印需要保留。</param>
    public static NutritionAssessmentSnapshot Capture(NutritionAssessmentRun run, bool includeUnanswered = true)
    {
        ArgumentNullException.ThrowIfNull(run);
        var definition = run.Definition;
        var evaluation = run.Evaluation;
        var responses = definition.Items
            .Where(item => evaluation.ApplicableItemCodes.Contains(item.Code))
            .Select(item =>
            {
                if (!run.Answers.TryGetValue(item.Code, out var answer))
                {
                    return includeUnanswered ? new AssessmentItemResponse
                    {
                        Item = AssessmentCoding(definition, $"{definition.Code}/item/{item.Code}", item.Prompt),
                        AnswerAbsentReason = DataAbsentReasonCode.NotAsked
                    } : null;
                }

                return new AssessmentItemResponse
                {
                    Item = AssessmentCoding(
                        definition,
                        $"{definition.Code}/item/{item.Code}",
                        item.Prompt),
                    Answer = AssessmentAnswer(definition, item, answer),
                    ScoreContribution = AssessmentScoreContribution(item, answer)
                };
            })
            .Where(response => response is not null)
            .Cast<AssessmentItemResponse>()
            .ToArray();

        return new NutritionAssessmentSnapshot
        {
            Subject = run.Subject,
            EffectiveAt = run.CompletedAt ?? run.LastModifiedAt,
            Instrument = new AssessmentInstrumentIdentity
            {
                Code = new Coding(
                    definition.CodeSystem,
                    definition.Code,
                    definition.Version,
                    definition.DisplayName),
                Version = definition.Version,
                Definition = new CanonicalReference(definition.DefinitionUri, definition.Version)
            },
            Responses = Array.AsReadOnly(responses),
            DerivedResults = Array.AsReadOnly(evaluation.Metrics.Select(metric => new NamedArchiveValue
            {
                Name = AssessmentCoding(
                    definition,
                    $"{definition.Code}/result/{metric.Code}",
                    metric.Display),
                Value = new DecimalArchiveValue(metric.Value)
            }).ToArray()),
            TotalScore = evaluation.TotalScore,
            TotalScoreAbsentReason = evaluation.TotalScore is null
                ? evaluation.IsComplete
                    ? DataAbsentReasonCode.NotApplicable
                    : DataAbsentReasonCode.NotEstablished
                : null,
            Interpretation = evaluation.Interpretation is { } interpretation
                ? AssessmentCoding(
                    definition,
                    $"{definition.Code}/interpretation/{interpretation.Code}",
                    interpretation.Display)
                : null
        };
    }

    private static Coding AssessmentCoding(
        NutritionAssessmentDefinition definition,
        string code,
        string display) => new(
            definition.CodeSystem,
            code,
            definition.Version,
            display);

    private static ArchiveValue AssessmentAnswer(
        NutritionAssessmentDefinition definition,
        NutritionAssessmentItem item,
        NutritionAssessmentAnswer answer) => answer switch
        {
            NutritionAssessmentSingleChoiceAnswer singleChoice =>
                new CodingArchiveValue(AssessmentOptionCoding(
                    definition,
                    item,
                    singleChoice.OptionCode)),
            NutritionAssessmentMultipleChoiceAnswer multipleChoice =>
                new CodingCollectionArchiveValue(item.Options
                    .Where(option => multipleChoice.OptionCodes.Contains(
                        option.Code,
                        StringComparer.Ordinal))
                    .Select(option => AssessmentOptionCoding(
                        definition,
                        item,
                        option.Code))),
            NutritionAssessmentDecimalAnswer number => new DecimalArchiveValue(number.Value),
            _ => throw new InvalidOperationException("量表包含无法映射的回答类型。")
        };

    private static decimal? AssessmentScoreContribution(
        NutritionAssessmentItem item,
        NutritionAssessmentAnswer answer) => answer switch
        {
            NutritionAssessmentSingleChoiceAnswer singleChoice =>
                item.Options.Single(option => string.Equals(
                    option.Code,
                    singleChoice.OptionCode,
                    StringComparison.Ordinal))
                .Score,
            NutritionAssessmentMultipleChoiceAnswer multipleChoice =>
                MultipleChoiceScoreContribution(item, multipleChoice),
            _ => null
        };

    private static decimal? MultipleChoiceScoreContribution(
        NutritionAssessmentItem item,
        NutritionAssessmentMultipleChoiceAnswer answer)
    {
        var selectedOptions = item.Options
            .Where(option => answer.OptionCodes.Contains(
                option.Code,
                StringComparer.Ordinal))
            .ToArray();
        return selectedOptions.Any(option => option.Score is null)
            ? null
            : selectedOptions.Sum(option => option.Score!.Value);
    }

    private static Coding AssessmentOptionCoding(
        NutritionAssessmentDefinition definition,
        NutritionAssessmentItem item,
        string optionCode)
    {
        var option = item.Options.Single(candidate => string.Equals(
            candidate.Code,
            optionCode,
            StringComparison.Ordinal));
        return AssessmentCoding(
            definition,
            $"{definition.Code}/item/{item.Code}/answer/{option.Code}",
            option.Display);
    }

}
