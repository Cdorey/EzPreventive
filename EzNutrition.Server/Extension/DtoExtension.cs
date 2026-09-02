using EzNutrition.Server.Data;
using EzNutrition.Server.Data.Entities;
using EzNutrition.Shared.Data.DTO;

namespace EzNutrition.Server.Extension
{
    public static class DtoExtension
    {
        public static UserDto ToDto(this ApplicationUser user)
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
        /// <returns>时间字段已归一化为 UTC 的 DTO 对象</returns>
        public static ProfessionalCertificationRequestDto ToDto(this ProfessionalCertificationRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            return new ProfessionalCertificationRequestDto
            {
                Id = request.Id,
                UserId = request.UserId,
                RequestTime = ToUtc(request.RequestTime),
                IdentityType = request.IdentityType,
                InstitutionName = request.InstitutionName,
                Status = request.Status,
                ProcessedTime = request.ProcessedTime is { } processedTime
                    ? ToUtc(processedTime)
                    : null,
                ProcessDetails = request.ProcessDetails,
                CertificateTicket = request.CertificateTicket,
                Remarks = request.Remarks
            };
        }

        /// <summary>
        /// 将后端时间归一化为 HTTP 契约使用的 UTC DateTime。
        /// </summary>
        /// <remarks>
        /// SQL Server datetime2 读回后 Kind 为 Unspecified；按照数据库 UTC 约定直接恢复为 UTC。
        /// </remarks>
        private static DateTime ToUtc(DateTime value)
        {
            return value.Kind switch
            {
                DateTimeKind.Local => value.ToUniversalTime(),
                DateTimeKind.Utc => value,
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };
        }

    }
}
