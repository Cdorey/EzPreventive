namespace EzNutrition.Application.Archives;

/// <summary>当前支持调阅的历史业务项目，不对应控件名称或序列化路径。</summary>
public enum ConsultationHistoryItem
{
    /// <summary>咨询时的身高。</summary>
    Height,
    /// <summary>咨询时的体重。</summary>
    Weight,
    /// <summary>专业人员采用的能量目标。</summary>
    AdoptedEnergy,
    /// <summary>膳食调查记录总能量。</summary>
    DietaryEnergy,
    /// <summary>SOAP 主观资料。</summary>
    Subjective,
    /// <summary>SOAP 客观资料。</summary>
    Objective,
    /// <summary>SOAP 问题评估。</summary>
    Assessment,
    /// <summary>SOAP 处理计划。</summary>
    Plan,
    /// <summary>已完成的营养建议正文。</summary>
    Advice
}

/// <summary>历史数量原值；保留单位身份和比较符，不在调阅时重新计算或换算。</summary>
public sealed record HistoryQuantity(decimal Value, string UnitSystem, string UnitCode, string UnitDisplay, string Comparator);

/// <summary>一项实际存在的历史事实；时间无法确定时不使用保存时间冒充。</summary>
public sealed record ConsultationHistoryFact(
    ConsultationHistoryItem Item,
    DateTimeOffset EffectiveAt,
    bool UsesConsultationTime,
    HistoryQuantity? Quantity,
    string? Text);

/// <summary>经过归属核验的一次既往咨询，只包含已保存且可解释的事实。</summary>
public sealed record ConsultationHistoryEntry(
    Guid DocumentId,
    Guid ConsultationId,
    DateTimeOffset ConsultationStartedAt,
    DateTimeOffset LastSavedAt,
    IReadOnlyList<ConsultationHistoryFact> Facts);

/// <summary>读取一份历史档案的结果；失败不会被伪装成没有历史值。</summary>
public sealed record ConsultationHistoryReadResult(ArchiveOperationResult Operation, ConsultationHistoryEntry? Entry);

/// <summary>
/// 当前复诊的历史上下文。首次请求时加载，同一工作区共享结果；不参与当前计算、AI 输入或档案组装。
/// </summary>
public sealed class ConsultationHistory(Guid patientId, Guid currentConsultationId)
{
    private Task? loadingTask;

    /// <summary>获取只读历史快照，按咨询发生时间倒序。</summary>
    public IReadOnlyList<ConsultationHistoryEntry> Entries { get; private set; } = [];

    /// <summary>获取是否正在读取档案。</summary>
    public bool IsLoading { get; private set; }

    /// <summary>获取是否完成本轮读取；部分失败时仍保留成功的记录。</summary>
    public bool IsLoaded { get; private set; }

    /// <summary>获取未能读取的档案数量。</summary>
    public int FailedCount { get; private set; }

    /// <summary>获取列表读取失败的说明。</summary>
    public string? Error { get; private set; }

    /// <summary>异步读取全部既往咨询；调用方使用工作区页面的生命周期令牌。</summary>
    public Task LoadAsync(IArchiveWorkflow workflow, CancellationToken cancellationToken)
    {
        if (loadingTask is { IsCompleted: false }) return loadingTask;
        if (IsLoaded) return Task.CompletedTask;
        return loadingTask = LoadCoreAsync(workflow, cancellationToken);
    }

    /// <summary>显式重新读取，供存储恢复或部分读取失败后重试。</summary>
    public Task ReloadAsync(IArchiveWorkflow workflow, CancellationToken cancellationToken)
    {
        if (IsLoading) return loadingTask!;
        IsLoaded = false;
        return LoadAsync(workflow, cancellationToken);
    }

    private async Task LoadCoreAsync(IArchiveWorkflow workflow, CancellationToken cancellationToken)
    {
        IsLoading = true;
        Error = null;
        FailedCount = 0;
        try
        {
            // 让单线程 WASM 有机会绘制加载状态；不把 Task.Run 当成浏览器工作线程。
            await Task.Delay(1, cancellationToken);
            var browse = await workflow.BrowseAsync(cancellationToken);
            if (!browse.Operation.IsSuccess)
            {
                Error = browse.Operation.Message;
                return;
            }

            var entries = new List<ConsultationHistoryEntry>();
            foreach (var record in browse.Records.Where(record => record.PatientId == patientId && record.DocumentId != currentConsultationId))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await workflow.ReadHistoryAsync(patientId, record.DocumentId, cancellationToken);
                if (result.Operation.IsSuccess && result.Entry is { } entry)
                {
                    if (entry.ConsultationId != currentConsultationId) entries.Add(entry);
                }
                else
                    FailedCount++;

                // 分份读取并交还事件循环；大档案自身的解码耗时仍由 codec 决定。
                await Task.Delay(1, cancellationToken);
            }

            Entries = Array.AsReadOnly(entries.OrderByDescending(entry => entry.LastSavedAt)
                .DistinctBy(entry => entry.ConsultationId).OrderByDescending(entry => entry.ConsultationStartedAt)
                .ThenBy(entry => entry.ConsultationId).ToArray());
            IsLoaded = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            Error = "既往咨询读取失败，请重试。";
            throw;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
