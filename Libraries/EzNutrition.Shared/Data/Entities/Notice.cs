namespace EzNutrition.Shared.Data.Entities
{
    public class Notice
    {
        public Guid NoticeId { get; set; } = Guid.NewGuid();

        public bool IsCoverLetter { get; set; } = false;

        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string PublisherId { get; set; } = string.Empty;

        public DateTime CreateTime { get; set; } = DateTime.Now;
    }
}
