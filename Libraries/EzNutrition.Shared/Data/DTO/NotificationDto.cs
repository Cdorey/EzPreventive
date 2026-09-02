using EzNutrition.Shared.Data.Entities;
using System.ComponentModel.DataAnnotations;

namespace EzNutrition.Shared.Data.DTO
{
    public class NotificationDto
    {
        [Required(ErrorMessage = "通知描述是必填项"), Display(Name = "通知正文")]
        public string NoticeDescription { get; set; } = string.Empty;

        [Display(Name = "通知标题")]
        public string? NoticeTitle { get; set; }

        /// <summary>
        /// 获取或设置待发布内容的类别。
        /// </summary>
        [EnumDataType(typeof(NoticeKind), ErrorMessage = "通知类型无效")]
        [Display(Name = "内容类型")]
        public NoticeKind Kind { get; set; } = NoticeKind.PostLoginAnnouncement;
    }
}
