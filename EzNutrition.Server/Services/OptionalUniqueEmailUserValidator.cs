using EzNutrition.Server.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace EzNutrition.Server.Services;

public sealed class OptionalUniqueEmailUserValidator(IdentityErrorDescriber errors)
    : IUserValidator<ApplicationUser>
{
    public async Task<IdentityResult> ValidateAsync(
        UserManager<ApplicationUser> manager,
        ApplicationUser user)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(user);

        var email = await manager.GetEmailAsync(user);
        if (string.IsNullOrEmpty(email))
        {
            return IdentityResult.Success;
        }

        if (email.Length > 256 || !new EmailAddressAttribute().IsValid(email))
        {
            return IdentityResult.Failed(errors.InvalidEmail(email));
        }

        var normalizedEmail = manager.NormalizeEmail(email);
        var conflictingUserIds = await manager.Users
            .Where(candidate =>
                candidate.NormalizedEmail == normalizedEmail &&
                candidate.Id != user.Id)
            .Select(candidate => candidate.Id)
            .Take(1)
            .ToListAsync();
        return conflictingUserIds.Count == 0
            ? IdentityResult.Success
            : IdentityResult.Failed(errors.DuplicateEmail(email));
    }
}
