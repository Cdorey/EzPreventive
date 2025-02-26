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
        public required List<UserClaimDto> Claims { get; set; }
    }
}