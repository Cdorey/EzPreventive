using Microsoft.Extensions.Options;

namespace EzNutrition.Server.Services.Settings;

/// <summary>校验 LLM 审计保留参数，不在配置层判定记录是否可以删除。</summary>
public sealed class LlmAuditCleanupOptionsValidator : IValidateOptions<LlmAuditCleanupOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, LlmAuditCleanupOptions options)
    {
        List<string> errors = [];
        if (options.RetentionDays is <= 0 || (options.Enabled && options.RetentionDays is null))
        {
            errors.Add("LLM 审计记录保留天数必须填写为正整数。");
        }
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
