using Microsoft.Extensions.Options;

namespace EzNutrition.Server.Services.Settings;

/// <summary>校验清理参数的完整性，不在配置层决定账号资格、保护范围或删除顺序。</summary>
public sealed class AccountCleanupOptionsValidator : IValidateOptions<AccountCleanupOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, AccountCleanupOptions options)
    {
        List<string> errors = [];
        ValidateInterval(options.CertificationSubmissionGraceDays,
            options.UnsubmittedCertificationCleanupEnabled, "认证申请宽限天数", errors);
        ValidateInterval(options.NonFormalAccountRetentionDays,
            options.NonFormalAccountCleanupEnabled, "非正式账号保留天数", errors);
        ValidateInterval(options.FormalAccountInactivityDays,
            options.InactiveFormalAccountCleanupEnabled, "账号连续未登录天数", errors);
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    /// <summary>允许未启用的规则暂不填写时间，但已填写的时间必须为正数。</summary>
    private static void ValidateInterval(int? value, bool required, string description, List<string> errors)
    {
        if (value is <= 0 || (required && value is null))
        {
            errors.Add($"{description}必须填写为正整数。");
        }
    }
}
