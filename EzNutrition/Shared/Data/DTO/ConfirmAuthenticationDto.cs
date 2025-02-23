using System.ComponentModel.DataAnnotations;

namespace EzNutrition.Shared.Data.DTO
{
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