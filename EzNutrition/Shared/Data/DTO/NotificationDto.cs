using System.ComponentModel.DataAnnotations;

namespace EzNutrition.Shared.Data.DTO
{
    public class NotificationDto
    {
        [Required(ErrorMessage = "通知描述是必填项"),Display(Name ="通知正文")]
        public string NoticeDescription { get; set; } = string.Empty;

        [Display(Name = "通知标题")]
        public string? NoticeTitle { get; set; }

        [Display(Name = "是否为封面信")]
        public bool IsCoverLetter { get; set; } = false;
    }
}