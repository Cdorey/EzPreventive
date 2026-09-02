using EzNutrition.Server.Data;

namespace EzNutrition.Server.Services;

public interface IAccountEmailSender
{
    Task SendConfirmationLinkAsync(
        ApplicationUser user,
        string email,
        string confirmationLink,
        CancellationToken cancellationToken = default);

    Task SendPasswordResetLinkAsync(
        ApplicationUser user,
        string email,
        string resetLink,
        CancellationToken cancellationToken = default);

    Task SendEmailChangeLinkAsync(
        ApplicationUser user,
        string newEmail,
        string confirmationLink,
        CancellationToken cancellationToken = default);

    Task SendEmailChangedNotificationAsync(
        ApplicationUser user,
        string previousEmail,
        string newEmail,
        CancellationToken cancellationToken = default);
}
