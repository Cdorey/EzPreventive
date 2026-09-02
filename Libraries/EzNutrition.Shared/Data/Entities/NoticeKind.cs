namespace EzNutrition.Shared.Data.Entities;

/// <summary>
/// 指定通知或政策文本的发布类别。
/// </summary>
public enum NoticeKind
{
    /// <summary>
    /// 登录后显示的公告；数值与原 <c>IsCoverLetter=false</c> 保持兼容。
    /// </summary>
    PostLoginAnnouncement = 0,

    /// <summary>
    /// 登录页面显示的公告；数值与原 <c>IsCoverLetter=true</c> 保持兼容。
    /// </summary>
    PreLoginAnnouncement = 1,

    /// <summary>
    /// 用户许可协议。
    /// </summary>
    UserAgreement = 2,

    /// <summary>
    /// 隐私条款。
    /// </summary>
    PrivacyPolicy = 3
}
