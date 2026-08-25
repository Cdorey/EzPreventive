namespace EzNutrition.Server.Data.Entities
{
    /// <summary>
    /// Ai生成调用的记录
    /// 用于安全审计
    /// 避免接口滥用
    /// </summary>
    public class PrescriptionGenerateRequest
    {
        /// <summary>
        /// 请求的唯一标识
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// 关联的用户ID（可以是 IdentityUser 的主键）
        /// </summary>
        public required string UserId { get; set; }

        /// <summary>
        /// 请求的提示词
        /// 已解码
        /// </summary>
        public required string Prompt { get; set; }

        /// <summary>
        /// 返回的思考链
        /// </summary>
        public string? ReasoningContent { get; set; }

        /// <summary>
        /// 返回的内容
        /// </summary>
        public string? Content { get; set; }

        /// <summary>
        /// 请求提交的时间
        /// </summary>
        public DateTime RequestTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 处理该请求的时间（生成完成时间）
        /// </summary>
        public DateTime ProcessedTime { get; set; }
    }
}
