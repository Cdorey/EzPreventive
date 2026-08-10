using EzNutrition.Server.Data;
using EzNutrition.Server.Services.Settings;
using EzNutrition.Shared.Data.DTO;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using System.Data;
using System.Text;

namespace EzNutrition.Server.Services;

public sealed class AccountSecurityService(
    UserManager<IdentityUser> userManager,
    ApplicationDbContext dbContext,
    IAccountEmailSender emailSender,
    IOptions<EmailSettings> options,
    ILogger<AccountSecurityService> logger)
{
    public const string GenericConfirmationResponse =
        "如果该邮箱对应一个尚未确认的账户，我们会发送确认邮件。";

    public const string GenericPasswordResetResponse =
        "如果该邮箱对应一个可找回的账户，我们会发送密码重置邮件。";

    public async Task SendEmailConfirmationAsync(
        IdentityUser user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            throw new InvalidOperationException("无法为没有电子邮箱的账户发送确认邮件。");
        }

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var link = BuildClientLink(
            "/account/confirm-email",
            new Dictionary<string, string?>
            {
                ["userId"] = user.Id,
                ["token"] = EncodeToken(token)
            });
        await emailSender.SendConfirmationLinkAsync(
            user,
            user.Email,
            link,
            cancellationToken);
    }

    public async Task RequestEmailConfirmationAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var user = await FindUniqueUserByEmailAsync(email, cancellationToken);
        if (user is null || user.EmailConfirmed || string.IsNullOrWhiteSpace(user.Email))
        {
            return;
        }

        try
        {
            await SendEmailConfirmationAsync(user, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to resend the confirmation email for user {UserId}.", user.Id);
        }
    }

    public async Task RequestPasswordResetAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var user = await FindUniqueUserByEmailAsync(email, cancellationToken);
        if (user is null || !user.EmailConfirmed || string.IsNullOrWhiteSpace(user.Email))
        {
            return;
        }

        try
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var link = BuildClientLink(
                "/account/reset-password",
                new Dictionary<string, string?>
                {
                    ["userId"] = user.Id,
                    ["token"] = EncodeToken(token)
                });
            await emailSender.SendPasswordResetLinkAsync(
                user,
                user.Email,
                link,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to send a password reset email for user {UserId}.", user.Id);
        }
    }

    public async Task<AccountSecurityResult> ConfirmEmailAsync(
        string userId,
        string encodedToken)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return AccountSecurityResult.Failure("邮箱确认链接无效或已过期。");
        }

        if (user.EmailConfirmed)
        {
            return AccountSecurityResult.Success("电子邮箱已经确认，可以直接登录。");
        }

        if (!TryDecodeToken(encodedToken, out var token))
        {
            return AccountSecurityResult.Failure("邮箱确认链接无效或已过期。");
        }

        var result = await userManager.ConfirmEmailAsync(user, token);
        return result.Succeeded
            ? AccountSecurityResult.Success("电子邮箱确认成功，可以登录了。")
            : AccountSecurityResult.Failure(DescribeIdentityFailure(result, "邮箱确认链接无效或已过期。"));
    }

    public async Task<AccountSecurityResult> ResetPasswordAsync(ResetPasswordDto request)
    {
        var user = await userManager.FindByIdAsync(request.UserId);
        if (user is null || !TryDecodeToken(request.Token, out var token))
        {
            return AccountSecurityResult.Failure("密码重置链接无效或已过期。");
        }

        var result = await userManager.ResetPasswordAsync(user, token, request.NewPassword);
        return result.Succeeded
            ? AccountSecurityResult.Success("密码已重置，请使用新密码登录。")
            : AccountSecurityResult.Failure(DescribeIdentityFailure(result, "密码重置链接无效或已过期。"));
    }

    public async Task<AccountSecurityResult> ChangePasswordAsync(
        string userId,
        ChangePasswordDto request)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return AccountSecurityResult.Failure("账户不存在或已失效。");
        }

        if (!await VerifyCurrentPasswordAsync(user, request.CurrentPassword))
        {
            return AccountSecurityResult.Failure("当前密码不正确或账户已暂时锁定。");
        }

        var result = await userManager.ChangePasswordAsync(
            user,
            request.CurrentPassword,
            request.NewPassword);
        return result.Succeeded
            ? AccountSecurityResult.Success("密码修改成功，请重新登录。")
            : AccountSecurityResult.Failure(DescribeIdentityFailure(result, "密码修改失败。"));
    }

    public async Task<AccountSecurityResult> RequestEmailChangeAsync(
        string userId,
        RequestEmailChangeDto request,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return AccountSecurityResult.Failure("账户不存在或已失效。");
        }

        if (!await VerifyCurrentPasswordAsync(user, request.CurrentPassword))
        {
            return AccountSecurityResult.Failure("当前密码不正确或账户已暂时锁定。");
        }

        var newEmail = request.NewEmail.Trim();
        if (string.Equals(
            userManager.NormalizeEmail(user.Email ?? string.Empty),
            userManager.NormalizeEmail(newEmail),
            StringComparison.Ordinal))
        {
            return AccountSecurityResult.Failure("新电子邮箱与当前邮箱相同。");
        }

        if (!await IsEmailAvailableForUserAsync(user.Id, newEmail, cancellationToken))
        {
            return AccountSecurityResult.Failure("该电子邮箱已被其他账户使用。");
        }

        var token = await userManager.GenerateChangeEmailTokenAsync(user, newEmail);
        var link = BuildClientLink(
            "/account/confirm-email-change",
            new Dictionary<string, string?>
            {
                ["userId"] = user.Id,
                ["newEmail"] = newEmail,
                ["token"] = EncodeToken(token)
            });

        await emailSender.SendEmailChangeLinkAsync(
            user,
            newEmail,
            link,
            cancellationToken);
        return AccountSecurityResult.Success("确认邮件已发送到新邮箱；确认前当前邮箱保持不变。");
    }

    public async Task<AccountSecurityResult> ConfirmEmailChangeAsync(
        ConfirmEmailChangeDto request,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(request.UserId);
        if (user is null || !TryDecodeToken(request.Token, out var token))
        {
            return AccountSecurityResult.Failure("邮箱变更链接无效或已过期。");
        }

        var newEmail = request.NewEmail.Trim();
        var snapshot = IdentityUserSnapshot.Capture(user);
        await using var transaction = await BeginIdentityTransactionAsync(cancellationToken);
        var previousEmail = user.Email;
        var changeResult = await userManager.ChangeEmailAsync(user, newEmail, token);
        if (!changeResult.Succeeded)
        {
            await RollbackAndRestoreAsync(transaction, user, snapshot, cancellationToken);
            return AccountSecurityResult.Failure(
                DescribeIdentityFailure(changeResult, "邮箱变更链接无效或已过期。"));
        }

        var stampResult = await userManager.UpdateSecurityStampAsync(user);
        if (!stampResult.Succeeded)
        {
            logger.LogError(
                "Email changed for user {UserId}, but updating the security stamp failed: {Errors}",
                user.Id,
                string.Join(", ", stampResult.Errors.Select(error => error.Code)));
            await RollbackAndRestoreAsync(transaction, user, snapshot, cancellationToken);
            return AccountSecurityResult.Failure("邮箱已验证，但账户安全状态更新失败；本次变更已撤销，请重试。");
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(previousEmail) &&
            !string.Equals(previousEmail, newEmail, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await emailSender.SendEmailChangedNotificationAsync(
                    user,
                    previousEmail,
                    newEmail,
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to notify the previous email for user {UserId}.", user.Id);
            }
        }

        return AccountSecurityResult.Success("电子邮箱修改成功，请重新登录。旧登录凭据已失效。");
    }

    public async Task<AccountSecurityResult> ChangePhoneNumberAsync(
        string userId,
        ChangePhoneNumberDto request)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return AccountSecurityResult.Failure("账户不存在或已失效。");
        }

        if (!await VerifyCurrentPasswordAsync(user, request.CurrentPassword))
        {
            return AccountSecurityResult.Failure("当前密码不正确或账户已暂时锁定。");
        }

        var snapshot = IdentityUserSnapshot.Capture(user);
        await using var transaction = await BeginIdentityTransactionAsync(CancellationToken.None);
        var phoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber)
            ? null
            : request.PhoneNumber.Trim();
        var result = await userManager.SetPhoneNumberAsync(user, phoneNumber);
        if (!result.Succeeded)
        {
            await RollbackAndRestoreAsync(transaction, user, snapshot, CancellationToken.None);
            return AccountSecurityResult.Failure(DescribeIdentityFailure(result, "手机号码修改失败。"));
        }

        var stampResult = await userManager.UpdateSecurityStampAsync(user);
        if (!stampResult.Succeeded)
        {
            logger.LogError(
                "Phone number changed for user {UserId}, but updating the security stamp failed: {Errors}",
                user.Id,
                string.Join(", ", stampResult.Errors.Select(error => error.Code)));
            await RollbackAndRestoreAsync(transaction, user, snapshot, CancellationToken.None);
            return AccountSecurityResult.Failure("账户安全状态更新失败；手机号码变更已撤销，请重试。");
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync();
        }

        return AccountSecurityResult.Success(
            phoneNumber is null
                ? "手机号码已清除，请重新登录。"
                : "手机号码已修改并标记为未验证，请重新登录。");
    }

    private async Task<IdentityUser?> FindUniqueUserByEmailAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = userManager.NormalizeEmail(email.Trim());
        var matches = await userManager.Users
            .Where(user => user.NormalizedEmail == normalizedEmail)
            .Take(2)
            .ToListAsync(cancellationToken);
        return matches.Count == 1 ? matches[0] : null;
    }

    private async Task<bool> IsEmailAvailableForUserAsync(
        string userId,
        string email,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = userManager.NormalizeEmail(email.Trim());
        return !await userManager.Users.AnyAsync(
            user => user.NormalizedEmail == normalizedEmail && user.Id != userId,
            cancellationToken);
    }

    private async Task<bool> VerifyCurrentPasswordAsync(
        IdentityUser user,
        string password)
    {
        if (userManager.SupportsUserLockout && await userManager.IsLockedOutAsync(user))
        {
            return false;
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            if (userManager.SupportsUserLockout)
            {
                await userManager.AccessFailedAsync(user);
            }

            return false;
        }

        if (userManager.SupportsUserLockout && await userManager.GetAccessFailedCountAsync(user) > 0)
        {
            await userManager.ResetAccessFailedCountAsync(user);
        }

        return true;
    }

    private async Task<IDbContextTransaction?> BeginIdentityTransactionAsync(
        CancellationToken cancellationToken)
    {
        return dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken)
            : null;
    }

    private async Task RollbackAndRestoreAsync(
        IDbContextTransaction? transaction,
        IdentityUser user,
        IdentityUserSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        if (transaction is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
        }

        snapshot.Restore(user);
        try
        {
            await dbContext.Entry(user).ReloadAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidOperationException or DbUpdateException)
        {
            logger.LogError(ex, "Unable to reload rolled-back Identity state for user {UserId}.", user.Id);
        }
    }

    private string BuildClientLink(string path, IDictionary<string, string?> query)
    {
        var clientUrl = options.Value.ClientUrl.TrimEnd('/');
        return QueryHelpers.AddQueryString($"{clientUrl}{path}", query);
    }

    private static string EncodeToken(string token) =>
        WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    private static bool TryDecodeToken(string encodedToken, out string token)
    {
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encodedToken));
            return true;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            token = string.Empty;
            return false;
        }
    }

    private static string DescribeIdentityFailure(IdentityResult result, string fallback)
    {
        if (result.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.InvalidToken)))
        {
            return fallback;
        }

        if (result.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.PasswordMismatch)))
        {
            return "当前密码不正确。";
        }

        if (result.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.DuplicateEmail)))
        {
            return "该电子邮箱已被其他账户使用。";
        }

        var descriptions = string.Join("；", result.Errors.Select(error => error.Description));
        return string.IsNullOrWhiteSpace(descriptions) ? fallback : descriptions;
    }

    private sealed record IdentityUserSnapshot(
        string? Email,
        string? NormalizedEmail,
        bool EmailConfirmed,
        string? PhoneNumber,
        bool PhoneNumberConfirmed,
        string? SecurityStamp,
        string? ConcurrencyStamp)
    {
        internal static IdentityUserSnapshot Capture(IdentityUser user) => new(
            user.Email,
            user.NormalizedEmail,
            user.EmailConfirmed,
            user.PhoneNumber,
            user.PhoneNumberConfirmed,
            user.SecurityStamp,
            user.ConcurrencyStamp);

        internal void Restore(IdentityUser user)
        {
            user.Email = Email;
            user.NormalizedEmail = NormalizedEmail;
            user.EmailConfirmed = EmailConfirmed;
            user.PhoneNumber = PhoneNumber;
            user.PhoneNumberConfirmed = PhoneNumberConfirmed;
            user.SecurityStamp = SecurityStamp;
            user.ConcurrencyStamp = ConcurrencyStamp;
        }
    }
}

public sealed record AccountSecurityResult(bool Succeeded, string Message)
{
    public static AccountSecurityResult Success(string message) => new(true, message);

    public static AccountSecurityResult Failure(string message) => new(false, message);
}
