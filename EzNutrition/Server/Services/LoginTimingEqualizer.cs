using Microsoft.AspNetCore.Identity;

namespace EzNutrition.Server.Services;

public sealed class LoginTimingEqualizer
{
    private readonly IdentityUser dummyUser = new() { UserName = "timing-equalizer" };
    private readonly PasswordHasher<IdentityUser> passwordHasher = new();

    public LoginTimingEqualizer()
    {
        dummyUser.PasswordHash = passwordHasher.HashPassword(
            dummyUser,
            Guid.NewGuid().ToString("N"));
    }

    public void Verify(string suppliedPassword)
    {
        passwordHasher.VerifyHashedPassword(
            dummyUser,
            dummyUser.PasswordHash!,
            suppliedPassword);
    }
}
