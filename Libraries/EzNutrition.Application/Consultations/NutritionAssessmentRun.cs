using System.Collections.ObjectModel;
using EzNutrition.Application.Archives;
using EzNutrition.Domain.Assessments;

namespace EzNutrition.Application.Consultations;

/// <summary>
/// 保存一次具体量表运行的回答、评估结果和可供咨询归档使用的稳定身份。
/// </summary>
public sealed class NutritionAssessmentRun
{
    private readonly INutritionAssessmentInstrument instrument;
    private readonly Dictionary<string, NutritionAssessmentAnswer> answers =
        new(StringComparer.Ordinal);
    private readonly ReadOnlyDictionary<string, NutritionAssessmentAnswer> readOnlyAnswers;

    internal NutritionAssessmentRun(
        INutritionAssessmentInstrument instrument,
        NutritionAssessmentSubject subject,
        NutritionAssessmentPerformerSnapshot? performer,
        ArchiveResourceIdentity archiveIdentity,
        DateTimeOffset createdAt,
        Guid? runId = null)
    {
        ArgumentNullException.ThrowIfNull(instrument);
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(archiveIdentity);

        this.instrument = instrument;
        Definition = instrument.Definition;
        Subject = subject;
        Performer = performer;
        ArchiveIdentity = archiveIdentity;
        RunId = runId ?? Guid.NewGuid();
        CreatedAt = createdAt;
        LastModifiedAt = createdAt;
        readOnlyAnswers =
            new ReadOnlyDictionary<string, NutritionAssessmentAnswer>(answers);
        Evaluation = instrument.Evaluate(readOnlyAnswers, subject);
    }

    /// <summary>获取本次量表实例的稳定运行标识。</summary>
    public Guid RunId { get; }

    /// <summary>获取当前量表的确切定义。</summary>
    public NutritionAssessmentDefinition Definition { get; }

    /// <summary>获取开始本次量表时采用的评估对象快照。</summary>
    public NutritionAssessmentSubject Subject { get; }

    /// <summary>获取开始本次量表时取得的可选调查人员身份快照。</summary>
    public NutritionAssessmentPerformerSnapshot? Performer { get; }

    /// <summary>获取当前已保存回答的只读视图。</summary>
    public IReadOnlyDictionary<string, NutritionAssessmentAnswer> Answers => readOnlyAnswers;

    /// <summary>获取当前回答对应的量表结果。</summary>
    public NutritionAssessmentEvaluation Evaluation { get; private set; }

    /// <summary>获取本次量表首次建立的时间。</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>获取回答或结果最后变更的时间。</summary>
    public DateTimeOffset LastModifiedAt { get; private set; }

    /// <summary>获取最近一次形成完整结果的时间；当前未完成时为空。</summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>获取映射档案资源时采用的稳定身份。</summary>
    public ArchiveResourceIdentity ArchiveIdentity { get; }

    /// <summary>
    /// 获取指定题目的当前选项编码。
    /// </summary>
    public string? GetAnswer(string itemCode) =>
        answers.TryGetValue(itemCode, out var value)
            ? (value as NutritionAssessmentSingleChoiceAnswer)?.OptionCode
            : null;

    /// <summary>
    /// 获取指定多选题当前选择的稳定选项编码。
    /// </summary>
    public IReadOnlyList<string> GetMultipleChoiceAnswer(string itemCode) =>
        answers.TryGetValue(itemCode, out var value)
            ? (value as NutritionAssessmentMultipleChoiceAnswer)?.OptionCodes ?? []
            : [];

    /// <summary>
    /// 获取指定数值题的当前回答。
    /// </summary>
    public decimal? GetDecimalAnswer(string itemCode) =>
        answers.TryGetValue(itemCode, out var value)
            ? (value as NutritionAssessmentDecimalAnswer)?.Value
            : null;

    /// <summary>
    /// 更新一道当前适用题目的回答并重新计算量表状态。
    /// </summary>
    /// <param name="itemCode">题目稳定编码。</param>
    /// <param name="optionCode">选项稳定编码。</param>
    /// <param name="modifiedAt">本次修改时间；未提供时采用当前 UTC 时间。</param>
    /// <returns>回答实际发生变化时返回 <see langword="true" />。</returns>
    public bool SetAnswer(
        string itemCode,
        string optionCode,
        DateTimeOffset? modifiedAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(optionCode);

        var item = GetApplicableItem(itemCode);
        if (item.ResponseType != NutritionAssessmentResponseType.SingleChoice)
        {
            throw new InvalidOperationException("指定题目不是单选题。");
        }

        if (!item.Options.Any(option =>
                string.Equals(option.Code, optionCode, StringComparison.Ordinal)))
        {
            throw new ArgumentException("指定选项不属于当前题目。", nameof(optionCode));
        }

        return SetTypedAnswer(
            item,
            new NutritionAssessmentSingleChoiceAnswer(optionCode),
            modifiedAt);
    }

    /// <summary>
    /// 更新一道当前适用多选题的完整选择集合并重新计算量表状态。
    /// </summary>
    public bool SetMultipleChoiceAnswer(
        string itemCode,
        IEnumerable<string> optionCodes,
        DateTimeOffset? modifiedAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemCode);
        ArgumentNullException.ThrowIfNull(optionCodes);

        var item = GetApplicableItem(itemCode);
        if (item.ResponseType != NutritionAssessmentResponseType.MultipleChoice)
        {
            throw new InvalidOperationException("指定题目不是多选题。");
        }

        var answer = new NutritionAssessmentMultipleChoiceAnswer(optionCodes);
        var selectedOptions = answer.OptionCodes
            .Select(optionCode => item.Options.SingleOrDefault(option =>
                string.Equals(option.Code, optionCode, StringComparison.Ordinal))
                ?? throw new ArgumentException(
                    "指定选项不属于当前题目。",
                    nameof(optionCodes)))
            .ToArray();
        if (selectedOptions.Length > 1 && selectedOptions.Any(option => option.IsExclusive))
        {
            throw new ArgumentException(
                "互斥选项不能与其他选项同时选择。",
                nameof(optionCodes));
        }

        return SetTypedAnswer(item, answer, modifiedAt);
    }

    /// <summary>
    /// 更新一道当前适用数值题的回答并重新计算量表状态。
    /// </summary>
    public bool SetDecimalAnswer(
        string itemCode,
        decimal value,
        DateTimeOffset? modifiedAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemCode);

        var item = GetApplicableItem(itemCode);
        if (item.ResponseType != NutritionAssessmentResponseType.Decimal)
        {
            throw new InvalidOperationException("指定题目不是数值题。");
        }

        if (item.MinimumValue is { } minimum && value < minimum
            || item.MaximumValue is { } maximum && value > maximum)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "数值回答超出当前题目允许的范围。");
        }

        return SetTypedAnswer(
            item,
            new NutritionAssessmentDecimalAnswer(value),
            modifiedAt);
    }

    /// <summary>
    /// 清除一道当前适用题目的回答并重新计算量表状态。
    /// </summary>
    public bool ClearAnswer(string itemCode, DateTimeOffset? modifiedAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemCode);
        var item = GetApplicableItem(itemCode);
        if (!answers.Remove(item.Code))
        {
            return false;
        }

        var changedAt = ValidateModifiedAt(modifiedAt);
        ReevaluateAndDiscardInapplicableAnswers();
        LastModifiedAt = changedAt;
        CompletedAt = Evaluation.IsComplete ? changedAt : null;
        return true;
    }

    private NutritionAssessmentItem GetApplicableItem(string itemCode)
    {
        var item = Definition.Items.SingleOrDefault(candidate =>
            string.Equals(candidate.Code, itemCode, StringComparison.Ordinal))
            ?? throw new ArgumentException("当前量表不存在指定题目。", nameof(itemCode));
        if (!Evaluation.ApplicableItemCodes.Contains(item.Code))
        {
            throw new InvalidOperationException("当前作答路径不适用指定题目。");
        }

        return item;
    }

    private bool SetTypedAnswer(
        NutritionAssessmentItem item,
        NutritionAssessmentAnswer answer,
        DateTimeOffset? modifiedAt)
    {
        if (answers.TryGetValue(item.Code, out var current)
            && AnswersEqual(current, answer))
        {
            return false;
        }

        var changedAt = ValidateModifiedAt(modifiedAt);
        answers[item.Code] = answer;
        ReevaluateAndDiscardInapplicableAnswers();
        LastModifiedAt = changedAt;
        CompletedAt = Evaluation.IsComplete ? changedAt : null;
        return true;
    }

    private DateTimeOffset ValidateModifiedAt(DateTimeOffset? modifiedAt)
    {
        var changedAt = modifiedAt ?? DateTimeOffset.UtcNow;
        if (changedAt < CreatedAt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(modifiedAt),
                changedAt,
                "量表修改时间不能早于量表建立时间。");
        }

        return changedAt;
    }

    private static bool AnswersEqual(
        NutritionAssessmentAnswer left,
        NutritionAssessmentAnswer right) => (left, right) switch
        {
            (NutritionAssessmentSingleChoiceAnswer first,
                NutritionAssessmentSingleChoiceAnswer second) =>
                string.Equals(first.OptionCode, second.OptionCode, StringComparison.Ordinal),
            (NutritionAssessmentMultipleChoiceAnswer first,
                NutritionAssessmentMultipleChoiceAnswer second) =>
                first.OptionCodes.SequenceEqual(second.OptionCodes, StringComparer.Ordinal),
            (NutritionAssessmentDecimalAnswer first,
                NutritionAssessmentDecimalAnswer second) => first.Value == second.Value,
            _ => false
        };

    private void ReevaluateAndDiscardInapplicableAnswers()
    {
        Evaluation = instrument.Evaluate(readOnlyAnswers, Subject);
        var inapplicableAnswers = answers.Keys
            .Where(itemCode => !Evaluation.ApplicableItemCodes.Contains(itemCode))
            .ToArray();
        if (inapplicableAnswers.Length == 0)
        {
            return;
        }

        foreach (var itemCode in inapplicableAnswers)
        {
            answers.Remove(itemCode);
        }

        Evaluation = instrument.Evaluate(readOnlyAnswers, Subject);
    }
}
