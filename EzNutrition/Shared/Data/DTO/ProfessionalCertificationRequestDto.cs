using System.ComponentModel.DataAnnotations;

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
        [Display(Name = "待审核")]
        Pending,   // 待审核
        [Display(Name = "已通过")]
        Approved,  // 已通过
        [Display(Name = "拒绝")]
        Rejected   // 拒绝
    }
}