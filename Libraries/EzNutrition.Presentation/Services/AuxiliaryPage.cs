namespace EzNutrition.Presentation.Services;

/// <summary>
/// 标识可以由客户端宿主独立承载的辅助页面。
/// </summary>
public enum AuxiliaryPage
{
    /// <summary>用户许可协议页面。</summary>
    UserAgreement = 1,

    /// <summary>隐私条款页面。</summary>
    PrivacyPolicy = 2
}

/// <summary>
/// 提供辅助页面的稳定路由与显示名称。
/// </summary>
public static class AuxiliaryPageExtensions
{
    /// <summary>
    /// 获取相对于客户端基地址的页面路径。
    /// </summary>
    /// <param name="page">辅助页面。</param>
    /// <returns>不以斜杠开头的相对路径。</returns>
    public static string GetRelativePath(this AuxiliaryPage page) => page switch
    {
        AuxiliaryPage.UserAgreement => "user-agreement",
        AuxiliaryPage.PrivacyPolicy => "privacy-policy",
        _ => throw new ArgumentOutOfRangeException(nameof(page), page, "未知的辅助页面。")
    };

    /// <summary>
    /// 获取适合用于原生窗口标题的页面名称。
    /// </summary>
    /// <param name="page">辅助页面。</param>
    /// <returns>页面的中文名称。</returns>
    public static string GetTitle(this AuxiliaryPage page) => page switch
    {
        AuxiliaryPage.UserAgreement => "用户许可协议",
        AuxiliaryPage.PrivacyPolicy => "隐私条款",
        _ => throw new ArgumentOutOfRangeException(nameof(page), page, "未知的辅助页面。")
    };
}
