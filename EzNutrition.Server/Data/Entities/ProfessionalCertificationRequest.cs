using EzNutrition.Shared.Data.DTO;
using System.ComponentModel.DataAnnotations;

namespace EzNutrition.Server.Data.Entities
{
    /// <summary>
    /// 用于存储用户专业认证请求的实体类
    /// </summary>
    public class ProfessionalCertificationRequest
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
        /// 请求提交的时间
        /// </summary>
        public DateTime RequestTime { get; set; }

        /// <summary>
        /// 专业身份类型，例如 "Doctor", "Nutritionist", "Teacher", "Student"
        /// </summary>
        public required string IdentityType { get; set; }

        /// <summary>
        /// 所属机构名称，例如医院、企业、学校名称
        /// </summary>
        public required string InstitutionName { get; set; }

        /// <summary>
        /// 请求的当前状态，例如 Pending、Approved、Rejected 等
        /// </summary>
        public RequestStatus Status { get; set; }

        /// <summary>
        /// 处理该请求的时间（审核完成时间）
        /// </summary>
        public DateTime? ProcessedTime { get; set; }

        /// <summary>
        /// 处理结果或审核意见
        /// </summary>
        public string? ProcessDetails { get; set; }

        /// <summary>
        /// 请求关联的证件照片的存储路径或标识
        /// </summary>
        public Guid? CertificateTicket { get; set; }

        /// <summary>
        /// 其他备注信息
        /// </summary>
        public string? Remarks { get; set; }
    }
}
