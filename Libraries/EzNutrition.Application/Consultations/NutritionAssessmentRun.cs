using System.Collections.ObjectModel;
using EzNutrition.Application.Archives;
using EzNutrition.Domain.Assessments;

namespace EzNutrition.Application.Consultations;

/// <summary>
/// 保存一次具体量表在咨询工作区中的回答、评估结果和稳定档案身份。
/// </summary>
public sealed class NutritionAssessmentRun
{
    private readonly INutritionAssessmentInstrument instrument;
    private readonly Dictionary<string, string> answers = new(StringComparer.Ordinal);
    private readonly ReadOnlyDictionary<string, string> readOnlyAnswers;

    internal NutritionAssessmentRun(
        INutritionAssessmentInstrument instrument,
        NutritionAssessmentSubject subject,
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
        ArchiveIdentity = archiveIdentity;
        RunId = runId ?? Guid.NewGuid();
        CreatedAt = createdAt;
        LastModifiedAt = createdAt;
        readOnlyAnswers = new ReadOnlyDictionary<string, string>(answers);
        Evaluation = instrument.Evaluate(readOnlyAnswers, subject);
    }

    /// <summary>获取本次量表实例的稳定运行标识。</summary>
    public Guid RunId { get; }

    /// <summary>获取当前量表的确切定义。</summary>
    public NutritionAssessmentDefinition Definition { get; }

    /// <summary>获取开始本次量表时采用的评估对象快照。</summary>
    public NutritionAssessmentSubject Subject { get; }

    /// <summary>获取当前已保存回答的只读视图。</summary>
    public IReadOnlyDictionary<string, string> Answers => readOnlyAnswers;

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
        answers.TryGetValue(itemCode, out var value) ? value : null;

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

        var item = Definition.Items.SingleOrDefault(candidate =>
            string.Equals(candidate.Code, itemCode, StringComparison.Ordinal))
            ?? throw new ArgumentException("当前量表不存在指定题目。", nameof(itemCode));
        if (!Evaluation.ApplicableItemCodes.Contains(item.Code))
        {
            throw new InvalidOperationException("当前作答路径不适用指定题目。");
        }

        if (!item.Options.Any(option =>
                string.Equals(option.Code, optionCode, StringComparison.Ordinal)))
        {
            throw new ArgumentException("指定选项不属于当前题目。", nameof(optionCode));
        }

        if (answers.TryGetValue(item.Code, out var current)
            && string.Equals(current, optionCode, StringComparison.Ordinal))
        {
            return false;
        }

        var changedAt = modifiedAt ?? DateTimeOffset.UtcNow;
        if (changedAt < CreatedAt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(modifiedAt),
                changedAt,
                "量表修改时间不能早于量表建立时间。");
        }

        answers[item.Code] = optionCode;
        ReevaluateAndDiscardInapplicableAnswers();
        LastModifiedAt = changedAt;
        CompletedAt = Evaluation.IsComplete ? changedAt : null;
        return true;
    }

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
