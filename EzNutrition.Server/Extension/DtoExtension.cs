using EzNutrition.Server.Data.Entities;
using EzNutrition.Shared.Data.DTO;
using Microsoft.AspNetCore.Identity;

namespace EzNutrition.Server.Extension
{
    public static class DtoExtension
    {
        public static UserDto ToDto(this IdentityUser user)
        {
            ArgumentNullException.ThrowIfNull(user);

            return new UserDto
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                NormalizedUserName = user.NormalizedUserName ?? string.Empty,
                Email = user.Email,
                EmailConfirmed = user.EmailConfirmed,
                PhoneNumber = user.PhoneNumber,
                PhoneNumberConfirmed = user.PhoneNumberConfirmed,
                TwoFactorEnabled = user.TwoFactorEnabled,
                LockoutEnabled = user.LockoutEnabled,
                AccessFailedCount = user.AccessFailedCount
            };
        }

        /// <summary>
        /// 根据实体对象创建 DTO
        /// </summary>
        /// <param name="request">专业认证请求实体</param>
        /// <returns>对应的 DTO 对象</returns>
        public static ProfessionalCertificationRequestDto ToDto(this ProfessionalCertificationRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            return new ProfessionalCertificationRequestDto
            {
                Id = request.Id,
                UserId = request.UserId,
                RequestTime = request.RequestTime,
                IdentityType = request.IdentityType,
                InstitutionName = request.InstitutionName,
                Status = request.Status,
                ProcessedTime = request.ProcessedTime,
                ProcessDetails = request.ProcessDetails,
                CertificateTicket = request.CertificateTicket,
                Remarks = request.Remarks
            };
        }

    }
}
