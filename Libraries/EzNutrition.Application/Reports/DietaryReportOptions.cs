namespace EzNutrition.Application.Reports;

/// <summary>固定本次膳食报告的展示范围，供预览和签发共用。</summary>
public sealed record DietaryReportOptions
{
    /// <summary>创建报告配置；排名为空时展示全部，并列食材保留相同位次。</summary>
    public DietaryReportOptions(int? contributionRankLimit = 10, bool includeDriReferences = true)
    {
        if (contributionRankLimit is <= 0)
            throw new ArgumentOutOfRangeException(nameof(contributionRankLimit), "食材贡献排名须为正整数，或选择显示全部。");
        ContributionRankLimit = contributionRankLimit;
        IncludeDriReferences = includeDriReferences;
    }

    /// <summary>获取每种宏量营养素展示的最高排名；为空时展示全部。</summary>
    public int? ContributionRankLimit { get; }

    /// <summary>获取是否展示独立的 DRIs 参考资料节。</summary>
    public bool IncludeDriReferences { get; }
}
