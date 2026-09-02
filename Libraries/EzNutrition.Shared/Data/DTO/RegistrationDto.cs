using System.ComponentModel.DataAnnotations;

namespace EzNutrition.Shared.Data.DTO
{
    public class RegistrationDto
    {
        /// <summary>
        /// 用户名，必填且应唯一
        /// </summary>
        [Required(ErrorMessage = "用户名不能为空"), Display(Name = "用户名")]
        public required string UserName { get; set; }

        /// <summary>
        /// 密码，必填，前端应做强度校验
        /// </summary>
        [Required(ErrorMessage = "密码不能为空"), Display(Name = "密码")]
        public required string Password { get; set; }

        /// <summary>
        /// 电子邮箱，必填，格式需要校验
        /// </summary>
        [Required(ErrorMessage = "电子邮箱不能为空")]
        [EmailAddress(ErrorMessage = "电子邮箱格式不正确"), Display(Name = "电子邮箱")]
        public required string Email { get; set; }

        /// <summary>
        /// 手机号码，可选，格式需校验
        /// </summary>
        [Phone(ErrorMessage = "手机号码格式不正确"), Display(Name = "手机号码")]
        public string? PhoneNumber { get; set; }

        /// <summary>
        /// 可选的专业身份信息，如果用户选择验证专业身份，则填写该信息；否则可为空
        /// </summary>
        public ProfessionalIdentityDto? ProfessionalIdentity { get; set; }
    }
}
