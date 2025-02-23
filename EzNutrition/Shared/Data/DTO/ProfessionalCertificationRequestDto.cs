namespace EzNutrition.Shared.Data.DTO
{
    /// <summary>
    /// 用户专业认证请求的 DTO 定义
    /// </summary>
    public class ProfessionalCertificationRequestDto
    {
        public Guid Id { get; set; }
        public required string UserId { get; set; }
        public DateTime RequestTime { get; set; }
        public required string IdentityType { get; set; }
        public required string InstitutionName { get; set; }
        public RequestStatus Status { get; set; }
        public DateTime? ProcessedTime { get; set; }
        public string? ProcessDetails { get; set; }
        public Guid? CertificateTicket { get; set; }
        public string? Remarks { get; set; }
    }

    /// <summary>
    /// 请求状态枚举，便于状态管理
    /// </summary>
    public enum RequestStatus
    {
        Pending,   // 待审核
        Approved,  // 已通过
        Rejected   // 拒绝
    }
}