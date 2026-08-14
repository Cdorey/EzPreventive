using System.Net;
using AntDesign;
using EzNutrition.Application.Archives;
using EzNutrition.Application.Consultations;
using EzNutrition.UI.Components;
using EzNutrition.UI.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace EzNutrition.Client.Tests.Components;

public sealed class ArchiveCenterRenderTests
{
    [Fact]
    public async Task Consultations_with_the_same_patient_identity_render_as_one_patient_group()
    {
        var patientId = Guid.NewGuid();
        var records = new[]
        {
            CreateRecord(Guid.NewGuid(), patientId, "张某", "第二次咨询", new DateTimeOffset(2026, 8, 14, 8, 0, 0, TimeSpan.Zero)),
            CreateRecord(Guid.NewGuid(), patientId, "张某", "第一次咨询", new DateTimeOffset(2026, 7, 1, 8, 0, 0, TimeSpan.Zero))
        };

        var html = WebUtility.HtmlDecode(await RenderAsync(records, includeFollowUpCallback: true));

        Assert.Equal(1, CountOccurrences(html, $"data-patient-id=\"{patientId:D}\""));
        Assert.Equal(1, CountOccurrences(html, "新建后续咨询"));
        Assert.Contains("2 次咨询", html, StringComparison.Ordinal);
        Assert.Contains("第一次咨询", html, StringComparison.Ordinal);
        Assert.Contains("第二次咨询", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Legacy_documents_with_equal_names_remain_separate_and_cannot_start_follow_up()
    {
        var records = new[]
        {
            CreateRecord(Guid.NewGuid(), null, "同名对象", "旧档案甲", new DateTimeOffset(2026, 8, 14, 8, 0, 0, TimeSpan.Zero)),
            CreateRecord(Guid.NewGuid(), null, "同名对象", "旧档案乙", new DateTimeOffset(2026, 7, 1, 8, 0, 0, TimeSpan.Zero))
        };

        var html = WebUtility.HtmlDecode(await RenderAsync(records, includeFollowUpCallback: true));

        Assert.Equal(2, CountOccurrences(html, "1 次咨询"));
        Assert.Equal(2, CountOccurrences(html, "旧版未关联档案"));
        Assert.DoesNotContain("新建后续咨询", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Destructive_controls_are_rendered_only_for_declared_capabilities()
    {
        var record = CreateRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "权限测试对象",
            "权限测试咨询",
            new DateTimeOffset(2026, 8, 14, 8, 0, 0, TimeSpan.Zero));

        var browseOnly = WebUtility.HtmlDecode(await RenderAsync([record], includeFollowUpCallback: false));
        var mutable = WebUtility.HtmlDecode(await RenderAsync(
            [record],
            includeFollowUpCallback: false,
            capabilities: ArchiveWorkflowCapabilities.Browse |
                          ArchiveWorkflowCapabilities.Delete |
                          ArchiveWorkflowCapabilities.Clear));

        Assert.DoesNotContain("清空档案库", browseOnly, StringComparison.Ordinal);
        Assert.DoesNotContain("删除 权限测试咨询", browseOnly, StringComparison.Ordinal);
        Assert.Contains("清空档案库", mutable, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"删除 权限测试咨询\"", mutable, StringComparison.Ordinal);
    }

    private static async Task<string> RenderAsync(
        IReadOnlyList<ArchiveRecordSummary> records,
        bool includeFollowUpCallback,
        ArchiveWorkflowCapabilities capabilities = ArchiveWorkflowCapabilities.Browse)
    {
        await using var services = new ServiceCollection()
            .AddLogging()
            .AddAntDesign()
            .AddSingleton<IJSRuntime, NoOpJsRuntime>()
            .AddSingleton<ILocalDateTimeFormatter>(new LocalDateTimeFormatter(TimeZoneInfo.Utc))
            .AddSingleton<IArchiveWorkflow>(new BrowseOnlyArchiveWorkflow(records, capabilities))
            .BuildServiceProvider();
        await using var renderer = new HtmlRenderer(
            services,
            services.GetRequiredService<ILoggerFactory>());

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = new Dictionary<string, object?>();
            if (includeFollowUpCallback)
            {
                parameters[nameof(ArchiveCenter.OnStartFollowUp)] =
                    EventCallback.Factory.Create<ArchivePatientContext>(new object(), _ => { });
            }

            var output = await renderer.RenderComponentAsync<ArchiveCenter>(
                ParameterView.FromDictionary(parameters));
            return output.ToHtmlString();
        });
    }

    private static ArchiveRecordSummary CreateRecord(
        Guid documentId,
        Guid? patientId,
        string subject,
        string title,
        DateTimeOffset startedAt) => new()
        {
            DocumentId = documentId,
            PatientId = patientId,
            SubjectDisplay = subject,
            Title = title,
            ConsultationStartedAt = startedAt,
            LastSavedAt = startedAt.AddHours(1)
        };

    private static int CountOccurrences(string value, string expected) =>
        value.Split(expected, StringSplitOptions.None).Length - 1;

    private sealed class BrowseOnlyArchiveWorkflow(
        IReadOnlyList<ArchiveRecordSummary> records,
        ArchiveWorkflowCapabilities capabilities) : IArchiveWorkflow
    {
        public ArchiveWorkflowCapabilities Capabilities { get; } = capabilities;

        public ValueTask<ArchiveBrowseResult> BrowseAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new ArchiveBrowseResult
            {
                Operation = Success("已读取档案列表。"),
                Records = records
            });

        public ValueTask<ArchiveOperationResult> SaveCurrentAsync(
            ConsultationWorkspace workspace,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<ArchiveOpenResult> OpenStoredAsync(
            Guid documentId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<ArchiveOperationResult> DeleteStoredAsync(
            Guid documentId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<ArchiveOperationResult> ClearStoredAsync(
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<ArchiveOpenResult> ImportAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<ArchiveOperationResult> ExportCurrentAsync(
            ConsultationWorkspace workspace,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        private static ArchiveOperationResult Success(string message) => new()
        {
            Status = ArchiveOperationStatus.Succeeded,
            Message = message
        };
    }

    private sealed class NoOpJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            ValueTask.FromResult(default(TValue)!);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args) => ValueTask.FromResult(default(TValue)!);
    }
}
