using System.ComponentModel.DataAnnotations;

namespace EzNutrition.Shared.Data.DTO
{
    public class ProfessionalIdentityDto
    {
        /// <summary>
        /// 专业身份类型，例如 "Doctor", "Nutritionist", "Teacher", "Student"
        /// </summary>
        [Required]
        public required string IdentityType { get; set; }

        /// <summary>
        /// 所属机构名称，例如医院、企业、学校名称
        /// </summary>
        [Required]
        public required string InstitutionName { get; set; }
    }

    public class ConfirmAuthenticationDto
    {
        [Required]
        public string UserId { get; set; }

        /// <summary>
        /// 审核是否通过
        /// </summary>
        public bool IsApproved { get; set; }

        /// <summary>
        /// 审核意见或备注
        /// </summary>
        public string Comments { get; set; }
    }
}