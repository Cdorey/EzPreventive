using System.Globalization;

namespace EzNutrition.Shared.Data.DTO
{
    public class UserInfoDto
    {
        public required string UserId { get; set; }
        public required string UserName { get; set; }
        public required string Email { get; set; }
        public required string PhoneNumber { get; set; }
        public required List<string> Roles { get; set; }
        public required List<ClaimDto> Claims { get; set; }
    }

    public class RoleClaimsDto
    {
        /// <summary>
        /// 角色名称
        /// </summary>
        public required string RoleName { get; set; }

        /// <summary>
        /// 该角色关联的所有 Claim
        /// </summary>
        public List<ClaimDto> Claims { get; set; } = new List<ClaimDto>();
    }

}