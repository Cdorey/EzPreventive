namespace EzNutrition.Shared.Data.Entities
{
    public class Notice
    {
        public Guid NoticeId { get; set; } = Guid.NewGuid();

        /// <summary>
        /// 获取或设置通知或政策文本的发布类别。
        /// </summary>
        public NoticeKind Kind { get; set; } = NoticeKind.PostLoginAnnouncement;

        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string PublisherId { get; set; } = string.Empty;

        /// <summary>
        /// 通知创建时间，按 UTC 保存。
        /// </summary>
        public DateTime CreateTime { get; set; } = DateTime.UtcNow;
    }
}
