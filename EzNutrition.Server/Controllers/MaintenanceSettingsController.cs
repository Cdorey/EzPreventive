using EzNutrition.Server.Services.Settings;
using EzNutrition.Shared.Data.DTO;
using EzNutrition.Shared.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace EzNutrition.Server.Controllers;

/// <summary>提供站点维护配置的管理员读取和保存接口。</summary>
[ApiController]
[Route("Admin/[controller]")]
[Authorize(Policy = PolicyList.Admin)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MaintenanceSettingsController(
    DatabaseSettings<AccountCleanupOptions> accountCleanup,
    DatabaseSettings<CertificationRequestCleanupOptions> certificationRequestCleanup,
    DatabaseSettings<LlmAuditCleanupOptions> llmAuditCleanup) : ControllerBase
{
    /// <summary>读取全部维护配置及各自的并发版本。</summary>
    [HttpGet]
    public async Task<ActionResult<MaintenanceSettingsDto>> Get(CancellationToken cancellationToken)
    {
        var account = await accountCleanup.GetAsync(cancellationToken);
        var certification = await certificationRequestCleanup.GetAsync(cancellationToken);
        var audit = await llmAuditCleanup.GetAsync(cancellationToken);

        return Ok(new MaintenanceSettingsDto(
            ToDto(account, ToDto),
            ToDto(certification, ToDto),
            ToDto(audit, ToDto)));
    }

    /// <summary>保存完整的账号清理配置。</summary>
    [HttpPut("account-cleanup")]
    public Task<IActionResult> SaveAccountCleanup(
        [FromBody] DatabaseSettingUpdateDto<AccountCleanupSettingsDto> request,
        CancellationToken cancellationToken) =>
        SaveAsync(accountCleanup, request, ToOptions, ToDto, cancellationToken);

    /// <summary>保存完整的认证申请清理配置。</summary>
    [HttpPut("certification-request-cleanup")]
    public Task<IActionResult> SaveCertificationRequestCleanup(
        [FromBody] DatabaseSettingUpdateDto<CertificationRequestCleanupSettingsDto> request,
        CancellationToken cancellationToken) =>
        SaveAsync(certificationRequestCleanup, request, ToOptions, ToDto, cancellationToken);

    /// <summary>保存完整的 LLM 审计记录清理配置。</summary>
    [HttpPut("llm-audit-cleanup")]
    public Task<IActionResult> SaveLlmAuditCleanup(
        [FromBody] DatabaseSettingUpdateDto<LlmAuditCleanupSettingsDto> request,
        CancellationToken cancellationToken) =>
        SaveAsync(llmAuditCleanup, request, ToOptions, ToDto, cancellationToken);

    /// <summary>校验并保存单组配置，将可恢复的业务错误转换为稳定 HTTP 结果。</summary>
    private async Task<IActionResult> SaveAsync<TOptions, TDto>(
        DatabaseSettings<TOptions> settings,
        DatabaseSettingUpdateDto<TDto> request,
        Func<TDto, TOptions> toOptions,
        Func<TOptions, TDto> toDto,
        CancellationToken cancellationToken)
        where TOptions : class, new()
        where TDto : class
    {
        try
        {
            var saved = await settings.SaveAsync(
                toOptions(request.Value),
                request.ExpectedVersion,
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                cancellationToken);
            return Ok(ToDto(saved, toDto));
        }
        catch (DatabaseSettingsConcurrencyException exception)
        {
            return Conflict(new DatabaseSettingConflictDto(exception.Key, exception.Message));
        }
        catch (OptionsValidationException exception)
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["value"] = exception.Failures.ToArray()
            }));
        }
    }

    /// <summary>保留持久化元数据并转换配置值。</summary>
    private static DatabaseSettingDto<TDto> ToDto<TOptions, TDto>(
        DatabaseSettingsValue<TOptions> source,
        Func<TOptions, TDto> convert)
        where TOptions : class
        where TDto : class =>
        new(convert(source.Value), source.Version, source.SchemaVersion,
            source.UpdatedAtUtc, source.UpdatedByUserId);

    private static AccountCleanupSettingsDto ToDto(AccountCleanupOptions value) => new()
    {
        UnsubmittedCertificationCleanupEnabled = value.UnsubmittedCertificationCleanupEnabled,
        CertificationSubmissionGraceDays = value.CertificationSubmissionGraceDays,
        NonFormalAccountCleanupEnabled = value.NonFormalAccountCleanupEnabled,
        NonFormalAccountRetentionDays = value.NonFormalAccountRetentionDays,
        InactiveFormalAccountCleanupEnabled = value.InactiveFormalAccountCleanupEnabled,
        FormalAccountInactivityDays = value.FormalAccountInactivityDays,
        SweepIntervalHours = value.SweepIntervalHours
    };

    private static AccountCleanupOptions ToOptions(AccountCleanupSettingsDto value) => new()
    {
        UnsubmittedCertificationCleanupEnabled = value.UnsubmittedCertificationCleanupEnabled,
        CertificationSubmissionGraceDays = value.CertificationSubmissionGraceDays,
        NonFormalAccountCleanupEnabled = value.NonFormalAccountCleanupEnabled,
        NonFormalAccountRetentionDays = value.NonFormalAccountRetentionDays,
        InactiveFormalAccountCleanupEnabled = value.InactiveFormalAccountCleanupEnabled,
        FormalAccountInactivityDays = value.FormalAccountInactivityDays,
        SweepIntervalHours = value.SweepIntervalHours
    };

    private static CertificationRequestCleanupSettingsDto ToDto(CertificationRequestCleanupOptions value) => new()
    {
        AutoRejectEnabled = value.AutoRejectEnabled,
        PendingTimeoutDays = value.PendingTimeoutDays,
        SweepIntervalHours = value.SweepIntervalHours
    };

    private static CertificationRequestCleanupOptions ToOptions(CertificationRequestCleanupSettingsDto value) => new()
    {
        AutoRejectEnabled = value.AutoRejectEnabled,
        PendingTimeoutDays = value.PendingTimeoutDays,
        SweepIntervalHours = value.SweepIntervalHours
    };

    private static LlmAuditCleanupSettingsDto ToDto(LlmAuditCleanupOptions value) => new()
    {
        Enabled = value.Enabled,
        RetentionDays = value.RetentionDays,
        SweepIntervalHours = value.SweepIntervalHours
    };

    private static LlmAuditCleanupOptions ToOptions(LlmAuditCleanupSettingsDto value) => new()
    {
        Enabled = value.Enabled,
        RetentionDays = value.RetentionDays,
        SweepIntervalHours = value.SweepIntervalHours
    };
}
