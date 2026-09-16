using AntDesign;
using EzNutrition.Shared.Data.DTO;
using Microsoft.AspNetCore.Components;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;

namespace EzNutrition.Presentation.Pages;

/// <summary>提供维护配置管理和账号清理预览、执行界面。</summary>
public partial class SiteMaintenance : ComponentBase, IDisposable
{
    private const string SettingsEndpoint = "Admin/MaintenanceSettings";
    private readonly CancellationTokenSource lifetimeCancellation = new();

    [Inject] private IHttpClientFactory HttpClientFactory { get; set; } = null!;
    [Inject] private IMessageService Message { get; set; } = null!;
    [Inject] private ILogger<SiteMaintenance> Logger { get; set; } = null!;

    private DatabaseSettingDto<CleanupScheduleSettingsDto>? scheduleSettings;
    private DatabaseSettingDto<AccountCleanupSettingsDto>? accountSettings;
    private DatabaseSettingDto<CertificationRequestCleanupSettingsDto>? certificationSettings;
    private DatabaseSettingDto<LlmAuditCleanupSettingsDto>? auditSettings;
    private AccountCleanupOperationDto? cleanupResult;
    private AccountCleanupRule selectedRule = AccountCleanupRule.CertificationNotRequested;
    private string scheduleStartTime = "03:30";
    private string? activeOperation;
    private bool isLoading = true;
    private bool loadFailed;
    private bool executionConfirmationVisible;
    private bool cleanupExecuting;

    private bool HasSettings =>
        scheduleSettings is not null && accountSettings is not null &&
        certificationSettings is not null && auditSettings is not null;

    private bool IsBusy => isLoading || activeOperation is not null || cleanupExecuting;

    private bool CanPreview =>
        !IsBusy && accountSettings?.Version is not null && SelectedRetentionDays is > 0;

    private bool CanExecute =>
        !IsBusy && cleanupResult is { DryRun: true, Items.Count: > 0 };

    private int? SelectedRetentionDays => selectedRule switch
    {
        AccountCleanupRule.CertificationNotRequested => accountSettings?.Value.CertificationSubmissionGraceDays,
        AccountCleanupRule.AccountWithoutRoles => accountSettings?.Value.NonFormalAccountRetentionDays,
        AccountCleanupRule.InactiveAccount => accountSettings?.Value.FormalAccountInactivityDays,
        _ => null
    };

    private string CleanupSummary => cleanupResult switch
    {
        null => string.Empty,
        { DryRun: true } result => $"{RuleLabel(result.Rule)}：找到 {result.Items.Count} 个候选账号，截止时间 {FormatCutoff(result.CutoffUtc)}。",
        var result => $"删除 {Count(result, AccountCleanupItemStatus.Deleted)}，跳过 {Count(result, AccountCleanupItemStatus.Skipped)}，失败 {Count(result, AccountCleanupItemStatus.Failed)}。"
    };

    /// <inheritdoc />
    protected override Task OnInitializedAsync() => LoadSettingsAsync();

    /// <summary>保存原生时间输入框返回的小时和分钟文本。</summary>
    private void UpdateScheduleStartTime(ChangeEventArgs args) =>
        scheduleStartTime = args.Value?.ToString() ?? string.Empty;

    /// <summary>账号规则或本地参数变化后，废弃基于旧输入生成的预览。</summary>
    private void InvalidateCleanupPreview() => cleanupResult = null;

    /// <summary>从服务端重新读取全部维护配置，并丢弃基于旧版本生成的预览。</summary>
    private async Task LoadSettingsAsync()
    {
        if (lifetimeCancellation.IsCancellationRequested)
        {
            return;
        }

        isLoading = true;
        loadFailed = false;
        cleanupResult = null;
        try
        {
            using var client = HttpClientFactory.CreateClient("Authorize");
            var settings = await client.GetFromJsonAsync<MaintenanceSettingsDto>(
                SettingsEndpoint,
                lifetimeCancellation.Token);
            if (settings is null)
            {
                loadFailed = true;
                return;
            }

            scheduleSettings = settings.CleanupSchedule;
            accountSettings = settings.AccountCleanup;
            certificationSettings = settings.CertificationRequestCleanup;
            auditSettings = settings.LlmAuditCleanup;
            scheduleStartTime = scheduleSettings.Value.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture);
        }
        catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            loadFailed = true;
            Logger.LogError(exception, "Unable to load maintenance settings.");
        }
        finally
        {
            isLoading = false;
        }
    }

    /// <summary>保存公共每日调度配置。</summary>
    private async Task SaveScheduleAsync()
    {
        if (scheduleSettings is null ||
            !TimeOnly.TryParseExact(
                scheduleStartTime,
                "HH:mm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var startTime))
        {
            await Message.ErrorAsync("请输入有效的每日启动时间。");
            return;
        }

        scheduleSettings.Value.StartTime = startTime;
        var saved = await SaveSettingAsync(
            "cleanup-schedule", scheduleSettings, "每日清理时间");
        if (saved is not null)
        {
            scheduleSettings = saved;
            scheduleStartTime = saved.Value.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>保存三条账号清理规则，并使已有预览失效。</summary>
    private async Task SaveAccountAsync()
    {
        if (accountSettings is null)
        {
            return;
        }

        var saved = await SaveSettingAsync("account-cleanup", accountSettings, "账号清理策略");
        if (saved is not null)
        {
            accountSettings = saved;
            cleanupResult = null;
        }
    }

    /// <summary>保存认证申请超时处理配置。</summary>
    private async Task SaveCertificationAsync()
    {
        if (certificationSettings is null)
        {
            return;
        }

        certificationSettings = await SaveSettingAsync(
            "certification-request-cleanup", certificationSettings, "认证申请清理策略")
            ?? certificationSettings;
    }

    /// <summary>保存 LLM 审计记录清理配置。</summary>
    private async Task SaveAuditAsync()
    {
        if (auditSettings is null)
        {
            return;
        }

        auditSettings = await SaveSettingAsync(
            "llm-audit-cleanup", auditSettings, "LLM 审计清理策略")
            ?? auditSettings;
    }

    /// <summary>以当前并发版本保存一个完整配置组。</summary>
    private async Task<DatabaseSettingDto<T>?> SaveSettingAsync<T>(
        string path,
        DatabaseSettingDto<T> current,
        string displayName) where T : class
    {
        activeOperation = path;
        try
        {
            using var client = HttpClientFactory.CreateClient("Authorize");
            using var response = await client.PutAsJsonAsync(
                $"{SettingsEndpoint}/{path}",
                new DatabaseSettingUpdateDto<T>(current.Value, current.Version),
                lifetimeCancellation.Token);
            if (!response.IsSuccessStatusCode)
            {
                await HandleFailureAsync(response, $"保存{displayName}");
                return null;
            }

            var saved = await response.Content.ReadFromJsonAsync<DatabaseSettingDto<T>>(
                lifetimeCancellation.Token);
            if (saved is null)
            {
                await Message.ErrorAsync($"{displayName}已提交，但服务器没有返回新版本，请重新加载。");
                return null;
            }

            await Message.SuccessAsync($"{displayName}已保存。");
            return saved;
        }
        catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Unable to save maintenance setting {SettingPath}.", path);
            await Message.ErrorAsync($"保存{displayName}失败，请稍后重试。");
            return null;
        }
        finally
        {
            activeOperation = null;
        }
    }

    /// <summary>根据当前已保存策略生成账号清理预览。</summary>
    private async Task PreviewCleanupAsync()
    {
        activeOperation = "cleanup-preview";
        cleanupResult = null;
        try
        {
            using var client = HttpClientFactory.CreateClient("Authorize");
            using var response = await client.PostAsJsonAsync(
                "Admin/AccountCleanup/preview",
                new AccountCleanupPreviewRequestDto(selectedRule),
                lifetimeCancellation.Token);
            if (!response.IsSuccessStatusCode)
            {
                await HandleFailureAsync(response, "生成账号清理预览");
                return;
            }

            cleanupResult = await response.Content.ReadFromJsonAsync<AccountCleanupOperationDto>(
                lifetimeCancellation.Token);
            if (cleanupResult is null)
            {
                await Message.ErrorAsync("服务器没有返回账号清理预览。");
            }
        }
        catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Unable to preview account cleanup rule {Rule}.", selectedRule);
            await Message.ErrorAsync("生成账号清理预览失败，请稍后重试。");
        }
        finally
        {
            activeOperation = null;
        }
    }

    /// <summary>打开最终删除确认，仅接受仍然有效且包含候选的预览。</summary>
    private void OpenExecutionConfirmation()
    {
        if (CanExecute)
        {
            executionConfirmationVisible = true;
        }
    }

    /// <summary>关闭账号删除确认窗口。</summary>
    private void CloseExecutionConfirmation()
    {
        if (!cleanupExecuting)
        {
            executionConfirmationVisible = false;
        }
    }

    /// <summary>使用预览返回的配置版本和截止时间执行账号删除。</summary>
    private async Task ExecuteCleanupAsync()
    {
        // 执行期间忽略重复确认，避免同时发起多轮删除。
        if (cleanupExecuting)
        {
            return;
        }

        if (cleanupResult is not { DryRun: true } preview || preview.Items.Count == 0)
        {
            executionConfirmationVisible = false;
            return;
        }

        cleanupExecuting = true;
        try
        {
            using var client = HttpClientFactory.CreateClient("Authorize");
            using var response = await client.PostAsJsonAsync(
                "Admin/AccountCleanup/execute",
                new AccountCleanupExecutionRequestDto(
                    preview.Rule,
                    preview.SettingsVersion,
                    preview.CutoffUtc),
                lifetimeCancellation.Token);
            if (!response.IsSuccessStatusCode)
            {
                cleanupResult = null;
                if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized
                    or HttpStatusCode.Forbidden or HttpStatusCode.Conflict)
                {
                    await HandleFailureAsync(response, "执行账号清理");
                }
                else
                {
                    await Message.WarningAsync("未能确认执行结果，请重新预览核对账号状态。");
                }
                return;
            }

            var result = await response.Content.ReadFromJsonAsync<AccountCleanupOperationDto>(
                lifetimeCancellation.Token);
            if (result is null)
            {
                await Message.ErrorAsync("服务器没有返回账号清理结果，请重新预览确认状态。");
                cleanupResult = null;
                return;
            }

            cleanupResult = result;
            executionConfirmationVisible = false;
            if (result.IsCanceled || result.Items.Any(item =>
                item.Status is AccountCleanupItemStatus.Failed or AccountCleanupItemStatus.WouldDelete ||
                item.CertificateFileCleanupFailures > 0))
            {
                await Message.WarningAsync($"清理存在未完成事项，请查看结果清单。{CleanupSummary}");
            }
            else
            {
                await Message.SuccessAsync($"清理完成。{CleanupSummary}");
            }
        }
        catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Unable to execute account cleanup rule {Rule}.", preview.Rule);
            cleanupResult = null;
            await Message.WarningAsync("未能确认执行结果，请重新预览核对账号状态。");
        }
        finally
        {
            cleanupExecuting = false;
            executionConfirmationVisible = false;
        }
    }

    /// <summary>显示稳定的 HTTP 失败说明；配置冲突后重新读取全部配置。</summary>
    private async Task HandleFailureAsync(HttpResponseMessage response, string action)
    {
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            DatabaseSettingConflictDto? conflict = null;
            try
            {
                conflict = await response.Content.ReadFromJsonAsync<DatabaseSettingConflictDto>(
                    lifetimeCancellation.Token);
            }
            catch (Exception exception) when (exception is HttpRequestException or System.Text.Json.JsonException)
            {
                Logger.LogDebug(exception, "Unable to read maintenance conflict response.");
            }

            await Message.WarningAsync(conflict?.Message ?? "配置已经变化，请重新确认。");
            await LoadSettingsAsync();
            return;
        }

        var message = response.StatusCode switch
        {
            HttpStatusCode.BadRequest => $"{action}失败，请检查当前配置和保留期限。",
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => $"{action}失败，当前登录状态或权限无效。",
            _ => $"{action}失败，请稍后重试。"
        };
        await Message.ErrorAsync(message);
    }

    /// <summary>返回配置版本的简短页面标记。</summary>
    private static string SettingVersionLabel<T>(DatabaseSettingDto<T>? setting) where T : class =>
        setting?.Version is { } version
            ? $"版本 {version.ToString("N")[..8]}"
            : "尚未保存";

    /// <summary>返回账号清理规则的中文名称。</summary>
    private static string RuleLabel(AccountCleanupRule rule) => rule switch
    {
        AccountCleanupRule.CertificationNotRequested => "从未申请认证",
        AccountCleanupRule.AccountWithoutRoles => "无合法角色",
        AccountCleanupRule.InactiveAccount => "长期未登录",
        _ => "未知规则"
    };

    /// <summary>返回账号清理状态的中文名称。</summary>
    private static string StatusLabel(AccountCleanupItemStatus status) => status switch
    {
        AccountCleanupItemStatus.WouldDelete => "待删除",
        AccountCleanupItemStatus.Deleted => "已删除",
        AccountCleanupItemStatus.Skipped => "已跳过",
        AccountCleanupItemStatus.Failed => "失败",
        _ => "未知"
    };

    /// <summary>返回账号清理状态对应的轻量视觉样式。</summary>
    private static string StatusClass(AccountCleanupItemStatus status) => status switch
    {
        AccountCleanupItemStatus.WouldDelete => "status-pill status-warning",
        AccountCleanupItemStatus.Deleted => "status-pill status-success",
        AccountCleanupItemStatus.Skipped => "status-pill",
        AccountCleanupItemStatus.Failed => "status-pill status-error",
        _ => "status-pill"
    };

    /// <summary>以明确的 UTC 格式显示清理截止时间。</summary>
    private static string FormatCutoff(DateTimeOffset? cutoff) =>
        cutoff?.ToUniversalTime().ToString(
            "yyyy-MM-dd HH:mm:ss 'UTC'",
            CultureInfo.InvariantCulture) ?? "—";

    private static int Count(AccountCleanupOperationDto result, AccountCleanupItemStatus status) =>
        result.Items.Count(item => item.Status == status);

    /// <inheritdoc />
    public void Dispose()
    {
        lifetimeCancellation.Cancel();
        lifetimeCancellation.Dispose();
    }
}
