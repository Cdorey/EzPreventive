using System.ComponentModel.DataAnnotations;

namespace EzNutrition.Shared.Data.DTO
{
    public class ProfessionalIdentityDto
    {
        /// <summary>
        /// 专业身份类型，例如 "Physician", "Nutritionist", "Teacher", "Student"
        /// </summary>
        [Required(ErrorMessage = "请选择认证类型"), Display(Name = "认证类型")]
        public required string IdentityType { get; set; }

        /// <summary>
        /// 所属机构名称，例如医院、企业、学校名称
        /// </summary>
        [Required(ErrorMessage = "所属机构名称不能为空"), Display(Name = "所属机构名称")]
        public required string InstitutionName { get; set; }
    }
}
