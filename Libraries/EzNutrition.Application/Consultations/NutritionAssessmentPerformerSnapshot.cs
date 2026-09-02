using EzNutrition.Shared.Identities;

namespace EzNutrition.Application.Consultations;

/// <summary>
/// 表示开始一次营养量表评估时取得的调查人员身份快照。
/// </summary>
/// <remarks>
/// 该快照保存历史行为事实，不随用户账号、认证资料或机构归属的后续变化而改变。
/// </remarks>
public sealed record NutritionAssessmentPerformerSnapshot
{
    private NutritionAssessmentPerformerSnapshot(
        string userId,
        string userName,
        string? realName,
        string? institutionName)
    {
        UserId = userId;
        UserName = userName;
        RealName = realName;
        InstitutionName = institutionName;
    }

    /// <summary>获取调查人员在 EzNutrition 中的稳定用户标识。</summary>
    public string UserId { get; }

    /// <summary>获取量表开始时的登录用户名。</summary>
    public string UserName { get; }

    /// <summary>获取量表开始时可用的经认证真实姓名。</summary>
    public string? RealName { get; }

    /// <summary>获取量表开始时可用的机构名称。</summary>
    public string? InstitutionName { get; }

    internal static NutritionAssessmentPerformerSnapshot FromUserInfo(IUserInfo userInfo)
    {
        ArgumentNullException.ThrowIfNull(userInfo);
        ArgumentException.ThrowIfNullOrWhiteSpace(userInfo.UserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(userInfo.UserName);
        return new NutritionAssessmentPerformerSnapshot(
            userInfo.UserId.Trim(),
            userInfo.UserName.Trim(),
            NormalizeOptional(userInfo.RealName),
            NormalizeOptional(userInfo.InstitutionName));
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
