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
    }
}
