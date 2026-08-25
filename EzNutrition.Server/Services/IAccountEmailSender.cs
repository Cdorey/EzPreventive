using Microsoft.AspNetCore.Identity;

namespace EzNutrition.Server.Services;

public interface IAccountEmailSender
{
    Task SendConfirmationLinkAsync(
        IdentityUser user,
        string email,
        string confirmationLink,
        CancellationToken cancellationToken = default);

    Task SendPasswordResetLinkAsync(
        IdentityUser user,
        string email,
        string resetLink,
        CancellationToken cancellationToken = default);

    Task SendEmailChangeLinkAsync(
        IdentityUser user,
        string newEmail,
        string confirmationLink,
        CancellationToken cancellationToken = default);

    Task SendEmailChangedNotificationAsync(
        IdentityUser user,
        string previousEmail,
        string newEmail,
        CancellationToken cancellationToken = default);
}
