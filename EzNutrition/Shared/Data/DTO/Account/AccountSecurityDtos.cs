using System.ComponentModel.DataAnnotations;

namespace EzNutrition.Shared.Data.DTO;

public sealed class ConfirmEmailDto
{
    [Required(ErrorMessage = "用户标识不能为空")]
    [StringLength(450)]
    public string UserId { get; set; } = string.Empty;

    [Required(ErrorMessage = "确认令牌不能为空")]
    [StringLength(4096)]
    public string Token { get; set; } = string.Empty;
}

public sealed class ResendEmailConfirmationDto
{
    [Required(ErrorMessage = "电子邮箱不能为空")]
    [EmailAddress(ErrorMessage = "电子邮箱格式不正确")]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;
}

public sealed class ForgotPasswordDto
{
    [Required(ErrorMessage = "电子邮箱不能为空")]
    [EmailAddress(ErrorMessage = "电子邮箱格式不正确")]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;
}

public sealed class ResetPasswordDto
{
    [Required(ErrorMessage = "用户标识不能为空")]
    [StringLength(450)]
    public string UserId { get; set; } = string.Empty;

    [Required(ErrorMessage = "重置令牌不能为空")]
    [StringLength(4096)]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "新密码不能为空")]
    [StringLength(256, MinimumLength = 6, ErrorMessage = "新密码至少需要 6 个字符")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "请再次输入新密码")]
    [Compare(nameof(NewPassword), ErrorMessage = "两次输入的密码不一致")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class ChangePasswordDto
{
    [Required(ErrorMessage = "当前密码不能为空")]
    [StringLength(256)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "新密码不能为空")]
    [StringLength(256, MinimumLength = 6, ErrorMessage = "新密码至少需要 6 个字符")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "请再次输入新密码")]
    [Compare(nameof(NewPassword), ErrorMessage = "两次输入的密码不一致")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class RequestEmailChangeDto
{
    [Required(ErrorMessage = "当前密码不能为空")]
    [StringLength(256)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "新电子邮箱不能为空")]
    [EmailAddress(ErrorMessage = "新电子邮箱格式不正确")]
    [StringLength(256)]
    public string NewEmail { get; set; } = string.Empty;
}

public sealed class ConfirmEmailChangeDto
{
    [Required(ErrorMessage = "用户标识不能为空")]
    [StringLength(450)]
    public string UserId { get; set; } = string.Empty;

    [Required(ErrorMessage = "新电子邮箱不能为空")]
    [EmailAddress(ErrorMessage = "新电子邮箱格式不正确")]
    [StringLength(256)]
    public string NewEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "确认令牌不能为空")]
    [StringLength(4096)]
    public string Token { get; set; } = string.Empty;
}

public sealed class ChangePhoneNumberDto
{
    [Required(ErrorMessage = "当前密码不能为空")]
    [StringLength(256)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Phone(ErrorMessage = "手机号码格式不正确")]
    [StringLength(64)]
    public string? PhoneNumber { get; set; }
}

public sealed class AccountOperationResultDto
{
    public required bool Success { get; init; }

    public required string Message { get; init; }
}
