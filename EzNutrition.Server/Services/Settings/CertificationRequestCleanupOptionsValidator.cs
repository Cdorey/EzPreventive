using Microsoft.Extensions.Options;

namespace EzNutrition.Server.Services.Settings;

/// <summary>校验认证申请超时参数，不在配置层执行审核状态转换。</summary>
public sealed class CertificationRequestCleanupOptionsValidator : IValidateOptions<CertificationRequestCleanupOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, CertificationRequestCleanupOptions options)
    {
        List<string> errors = [];
        if (options.PendingTimeoutDays is <= 0 || (options.AutoRejectEnabled && options.PendingTimeoutDays is null))
        {
            errors.Add("待审核申请超时天数必须填写为正整数。");
        }
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
