using EzNutrition.Server.Services.Maintenance;
using EzNutrition.Server.Services.Settings;
using EzNutrition.Shared.Data.DTO;
using EzNutrition.Shared.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EzNutrition.Server.Controllers;

/// <summary>提供按持久化策略预览和执行账号清理的管理员接口。</summary>
/// <remarks>人工预览和执行不受自动清理开关限制，但所选规则必须已经配置保留天数。</remarks>
[ApiController]
[Route("Admin/[controller]")]
[Authorize(Policy = PolicyList.Admin)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountCleanupController(
    AccountCleanupService cleanup,
    DatabaseSettings<AccountCleanupOptions> settings,
    TimeProvider timeProvider) : ControllerBase
{
    /// <summary>按当前配置预览一条账号清理规则，不修改账号或关联数据。</summary>
    [HttpPost("preview")]
    public Task<IActionResult> Preview(
        [FromBody] AccountCleanupPreviewRequestDto request,
        CancellationToken cancellationToken) =>
        RunAsync(request.Rule, dryRun: true, null, null, cancellationToken);

    /// <summary>按已经读取或预览的配置版本执行一条账号清理规则。</summary>
    [HttpPost("execute")]
    public Task<IActionResult> Execute(
        [FromBody] AccountCleanupExecutionRequestDto request,
        CancellationToken cancellationToken) =>
        RunAsync(request.Rule, dryRun: false, request.ExpectedSettingsVersion, request.CutoffUtc, cancellationToken);

    /// <summary>读取并固定本轮策略；执行前拒绝过期的配置版本。</summary>
    private async Task<IActionResult> RunAsync(
        AccountCleanupRule rule,
        bool dryRun,
        Guid? expectedSettingsVersion,
        DateTimeOffset? requestedCutoffUtc,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(rule))
        {
            return InvalidRequest("rule", "账号清理规则无效。");
        }
        if (!dryRun && (expectedSettingsVersion is null || expectedSettingsVersion == Guid.Empty))
        {
            return InvalidRequest("expectedSettingsVersion", "执行清理必须提供有效的配置版本。");
        }

        var current = await settings.GetAsync(cancellationToken);
        if (current.Version is not { } settingsVersion)
        {
            return InvalidRequest("settings", "账号清理参数尚未保存，请先完成配置。");
        }
        if (!dryRun && expectedSettingsVersion != settingsVersion)
        {
            return Conflict(new DatabaseSettingConflictDto(
                AccountCleanupOptions.SectionName,
                "账号清理配置已发生变化，请重新预览后再执行。"));
        }

        var retentionDays = rule switch
        {
            AccountCleanupRule.CertificationNotRequested => current.Value.CertificationSubmissionGraceDays,
            AccountCleanupRule.AccountWithoutRoles => current.Value.NonFormalAccountRetentionDays,
            AccountCleanupRule.InactiveAccount => current.Value.FormalAccountInactivityDays,
            _ => null
        };
        if (retentionDays is not > 0)
        {
            return InvalidRequest("settings", "所选清理规则尚未配置有效的保留天数。");
        }

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        // 极大的保留期意味着没有候选；使用最小时间后一刻避免日期减法溢出。
        var configuredCutoffUtc = retentionDays.Value >= (now - DateTimeOffset.MinValue).TotalDays
            ? DateTimeOffset.MinValue.AddTicks(1)
            : now.AddDays(-retentionDays.Value);
        var cutoffUtc = configuredCutoffUtc;
        if (!dryRun)
        {
            if (requestedCutoffUtc is not { } requested ||
                requested <= DateTimeOffset.MinValue || requested > configuredCutoffUtc)
            {
                return InvalidRequest("cutoffUtc", "执行清理必须使用预览返回且未扩大清理范围的截止时间。");
            }
            cutoffUtc = requested.ToUniversalTime();
        }
        var result = rule switch
        {
            AccountCleanupRule.CertificationNotRequested => await cleanup.DeleteAccountsWithoutRolesAsync(
                cutoffUtc, onlyWithoutApplications: true, dryRun, cancellationToken),
            AccountCleanupRule.AccountWithoutRoles => await cleanup.DeleteAccountsWithoutRolesAsync(
                cutoffUtc, onlyWithoutApplications: false, dryRun, cancellationToken),
            AccountCleanupRule.InactiveAccount => await cleanup.DeleteInactiveAccountsAsync(
                cutoffUtc, dryRun, cancellationToken),
            _ => throw new InvalidOperationException("已经校验的账号清理规则无法解析。")
        };

        return Ok(new AccountCleanupOperationDto(
            rule,
            settingsVersion,
            result.CutoffUtc,
            result.DryRun,
            result.Items.Select(ToDto).ToArray(),
            result.IsCanceled));
    }

    /// <summary>返回字段级参数或配置错误。</summary>
    private ActionResult InvalidRequest(string field, string message) =>
        ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            [field] = [message]
        }));

    /// <summary>将内部执行结果转换为不暴露服务实体的 HTTP 契约。</summary>
    private static AccountCleanupItemDto ToDto(AccountCleanupItem item) => new(
        item.UserId,
        item.UserName,
        item.Status switch
        {
            AccountCleanupStatus.WouldDelete => AccountCleanupItemStatus.WouldDelete,
            AccountCleanupStatus.Deleted => AccountCleanupItemStatus.Deleted,
            AccountCleanupStatus.Skipped => AccountCleanupItemStatus.Skipped,
            AccountCleanupStatus.Failed => AccountCleanupItemStatus.Failed,
            _ => throw new InvalidOperationException("未知的账号清理结果状态。")
        },
        item.Reason,
        item.CertificateFileCleanupFailures);
}
