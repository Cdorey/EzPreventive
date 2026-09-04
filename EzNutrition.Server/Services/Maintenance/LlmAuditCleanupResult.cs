namespace EzNutrition.Server.Services.Maintenance;

/// <summary>返回一轮 LLM 审计记录清理的计数结果。</summary>
/// <param name="CutoffUtc">本轮固定使用的 UTC 截止时间。</param>
/// <param name="DryRun">是否仅统计候选记录。</param>
/// <param name="MatchedRecords">符合过期条件的记录数量。</param>
/// <param name="DeletedRecords">从数据库实际删除的记录数量。</param>
/// <param name="ConfigurationChanged">自动扫描是否因配置变化而停止。</param>
public sealed record LlmAuditCleanupResult(
    DateTimeOffset CutoffUtc,
    bool DryRun,
    int MatchedRecords,
    int DeletedRecords,
    bool ConfigurationChanged = false);
