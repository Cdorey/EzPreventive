namespace EzNutrition.Application.Reports;

/// <summary>固定本次能量报告的展示节段；完整能量快照随报告保存。</summary>
public sealed record EnergyReportOptions(bool IncludeTotalEnergy = true, bool IncludeAllocation = true,
    bool IncludeExchanges = true)
{
    /// <summary>报告至少包含一个节段。</summary>
    public void Validate()
    {
        if (!IncludeTotalEnergy && !IncludeAllocation && !IncludeExchanges)
            throw new InvalidOperationException("请至少选择一个报告节段。");
    }
}
