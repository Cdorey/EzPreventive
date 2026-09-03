using EzNutrition.Server.Data;
using EzNutrition.Server.Data.Entities;
using EzNutrition.Server.Extension;
using EzNutrition.Server.Services;
using EzNutrition.Server.Services.Maintenance;
using EzNutrition.Server.Services.Settings;
using EzNutrition.Shared.Data.DTO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EzNutrition.Server.Tests.Services;

public sealed class CertificationRequestCleanupTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Preview_uses_original_submission_and_has_no_count_limit_or_side_effects()
    {
        await using var host = new TestHost();
        var cutoff = Now.AddDays(-7);
        var candidates = Enumerable.Range(0, 1001).Select(index => Request(cutoff.AddDays(-1))).ToArray();
        candidates[0].ProcessedTime = Now.UtcDateTime;
        await host.SeedAsync(candidates.Concat([
            Request(cutoff), Request(cutoff.AddSeconds(1)), Request(DateTimeOffset.MinValue),
            Request(cutoff.AddDays(-1), RequestStatus.Approved),
            Request(cutoff.AddDays(-1), RequestStatus.Rejected)]).ToArray());

        var result = await host.Cleanup.RejectExpiredAsync(cutoff.ToOffset(TimeSpan.FromHours(8)));

        Assert.True(result.DryRun);
        Assert.Equal(TimeSpan.Zero, result.CutoffUtc.Offset);
        Assert.Equal(1001, result.Items.Count);
        Assert.All(result.Items, item => Assert.Equal(CertificationRequestCleanupStatus.WouldReject, item.Status));
        await using var db = host.CreateContext();
        Assert.Equal(1004, await db.ProfessionalCertificationRequests.CountAsync(item => item.Status == RequestStatus.Pending));
        Assert.Equal(candidates[0].Version, (await db.ProfessionalCertificationRequests.FindAsync(candidates[0].Id))!.Version);
    }

    [Fact]
    public async Task Execute_rejects_only_expired_pending_requests_and_preserves_submission_and_latest_remarks()
    {
        await using var host = new TestHost();
        var expired = Request(Now.AddDays(-10));
        expired.Remarks = "管理员正在联系申请人";
        expired.ProcessedTime = Now.AddHours(-1).UtcDateTime;
        var fresh = Request(Now);
        var approved = Request(Now.AddDays(-10), RequestStatus.Approved);
        await host.SeedAsync(expired, fresh, approved);

        var result = await host.Cleanup.RejectExpiredAsync(Now.AddDays(-7), dryRun: false);

        Assert.Equal(CertificationRequestCleanupStatus.Rejected, Assert.Single(result.Items).Status);
        var saved = await host.ReadAsync(expired.Id);
        Assert.Equal(RequestStatus.Rejected, saved.Status);
        Assert.Equal(expired.RequestTime, saved.RequestTime);
        Assert.Equal(Now.UtcDateTime, saved.ProcessedTime);
        Assert.Equal(expired.Remarks, saved.Remarks);
        Assert.Equal("申请超过待审核期限，系统自动拒绝。", saved.ProcessDetails);
        Assert.NotEqual(expired.Version, saved.Version);
        Assert.Null(saved.CertificateTicket);
        Assert.Equal(RequestStatus.Pending, (await host.ReadAsync(fresh.Id)).Status);
        Assert.Equal(RequestStatus.Approved, (await host.ReadAsync(approved.Id)).Status);
        Assert.Empty((await host.Cleanup.RejectExpiredAsync(Now.AddDays(-7), false)).Items);
    }

    [Theory]
    [InlineData(RequestStatus.Approved, RequestStatus.Pending)]
    [InlineData(RequestStatus.Approved, RequestStatus.Approved)]
    [InlineData(RequestStatus.Approved, RequestStatus.Rejected)]
    [InlineData(RequestStatus.Rejected, RequestStatus.Pending)]
    [InlineData(RequestStatus.Rejected, RequestStatus.Approved)]
    [InlineData(RequestStatus.Rejected, RequestStatus.Rejected)]
    public async Task Admin_can_replace_completed_decisions_but_cannot_reopen_as_pending(RequestStatus current, RequestStatus target)
    {
        await using var host = new TestHost();
        var request = Request(Now.AddDays(-10), current);
        request.ProcessedTime = Now.AddDays(-1).UtcDateTime;
        await host.SeedAsync(request);

        var result = await host.Review.UpdateAsync(request.Id, request.Version, target, "新决定", "新备注");

        Assert.Equal(target == RequestStatus.Pending ? CertificationReviewStatus.Conflict : CertificationReviewStatus.Updated, result.Status);
        var saved = await host.ReadAsync(request.Id);
        if (target == RequestStatus.Pending)
        {
            Assert.Equal(current, saved.Status);
            Assert.Equal(request.Version, saved.Version);
            Assert.Equal(request.ProcessedTime, saved.ProcessedTime);
        }
        else
        {
            Assert.Equal(target, saved.Status);
            Assert.NotEqual(request.Version, saved.Version);
            Assert.Equal(Now.UtcDateTime, saved.ProcessedTime);
            Assert.Equal("新决定", saved.ProcessDetails);
        }
    }

    [Theory]
    [InlineData(RequestStatus.Pending, CertificationReviewStatus.Conflict)]
    [InlineData(RequestStatus.Approved, CertificationReviewStatus.Updated)]
    [InlineData(RequestStatus.Rejected, CertificationReviewStatus.Updated)]
    public async Task Stale_decisions_can_retry_but_stale_note_edits_do_not_overwrite(RequestStatus target, CertificationReviewStatus expected)
    {
        await using var host = new TestHost();
        var request = Request(Now.AddDays(-10));
        await host.SeedAsync(request);
        await host.ChangeAsync(request.Id, item => item.Remarks = "较新的备注");

        var result = await host.Review.UpdateAsync(request.Id, request.Version, target, "意见", "原备注");

        Assert.Equal(expected, result.Status);
        if (target == RequestStatus.Pending)
        {
            Assert.Equal("较新的备注", (await host.ReadAsync(request.Id)).Remarks);
        }
    }

    [Fact]
    public async Task A_competing_approval_wins_at_database_save_and_timeout_does_not_overwrite_it()
    {
        await using var host = new TestHost();
        var request = Request(Now.AddDays(-10));
        await host.SeedAsync(request);
        CertificationReviewResult? winner = null;
        host.Interceptor.BeforeSave = async () => winner = await host.Review.UpdateAsync(
            request.Id, request.Version, RequestStatus.Approved, "管理员通过", null);

        var result = await host.Cleanup.RejectExpiredAsync(Now.AddDays(-7), false);

        Assert.Equal(CertificationReviewStatus.Updated, winner!.Status);
        Assert.Equal(CertificationRequestCleanupStatus.Skipped, Assert.Single(result.Items).Status);
        var saved = await host.ReadAsync(request.Id);
        Assert.Equal(RequestStatus.Approved, saved.Status);
        Assert.Equal("管理员通过", saved.ProcessDetails);
        Assert.Equal(2, host.Interceptor.SaveAttempts);
    }

    [Fact]
    public async Task Two_timeout_sweeps_only_complete_the_request_once()
    {
        await using var host = new TestHost();
        await host.SeedAsync(Request(Now.AddDays(-10)));
        CertificationRequestCleanupResult? winner = null;
        host.Interceptor.BeforeSave = async () => winner = await host.Cleanup.RejectExpiredAsync(Now.AddDays(-7), false);

        var loser = await host.Cleanup.RejectExpiredAsync(Now.AddDays(-7), false);

        Assert.Equal(CertificationRequestCleanupStatus.Rejected, Assert.Single(winner!.Items).Status);
        Assert.Equal(CertificationRequestCleanupStatus.Skipped, Assert.Single(loser.Items).Status);
    }

    [Fact]
    public async Task Admin_approval_silently_retries_over_a_timeout_rejection_committed_after_its_read()
    {
        await using var host = new TestHost();
        var request = Request(Now.AddDays(-10));
        await host.SeedAsync(request);
        CertificationRequestCleanupResult? sweep = null;
        host.Interceptor.BeforeSave = async () => sweep = await host.Cleanup.RejectExpiredAsync(Now.AddDays(-7), false);

        var result = await host.Review.UpdateAsync(request.Id, request.Version, RequestStatus.Approved, "管理员确认通过", "人工备注");

        Assert.Equal(CertificationRequestCleanupStatus.Rejected, Assert.Single(sweep!.Items).Status);
        Assert.Equal(CertificationReviewStatus.Updated, result.Status);
        var saved = await host.ReadAsync(request.Id);
        Assert.Equal(RequestStatus.Approved, saved.Status);
        Assert.Equal("管理员确认通过", saved.ProcessDetails);
        Assert.Equal("人工备注", saved.Remarks);
        Assert.Equal(request.RequestTime, saved.RequestTime);
        Assert.Null(saved.CertificateTicket);
        Assert.Equal(saved.Version, result.Version);
        Assert.Equal(3, host.Interceptor.SaveAttempts);
    }

    [Fact]
    public async Task Timeout_skips_a_changed_initial_version_without_attempting_a_save()
    {
        await using var host = new TestHost();
        var request = Request(Now.AddDays(-10));
        await host.SeedAsync(request);
        await host.ChangeAsync(request.Id, item => item.Remarks = "管理员已修改备注");

        var result = await host.Review.RejectExpiredAsync(request.Id, request.Version, Now.AddDays(-7));

        Assert.Equal(CertificationReviewStatus.Conflict, result.Status);
        Assert.Equal(0, host.Interceptor.SaveAttempts);
        Assert.Equal(RequestStatus.Pending, (await host.ReadAsync(request.Id)).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Timeout_skips_save_conflicts_without_retry_even_when_request_remains_expired(bool becomesFresh)
    {
        await using var host = new TestHost();
        var request = Request(Now.AddDays(-10));
        await host.SeedAsync(request);
        host.Interceptor.BeforeSave = () => host.ChangeAsync(request.Id, item =>
        {
            item.Remarks = "并发更新的备注";
            if (becomesFresh) item.RequestTime = Now.UtcDateTime;
        });

        var result = await host.Cleanup.RejectExpiredAsync(Now.AddDays(-7), false);

        Assert.Equal(CertificationRequestCleanupStatus.Skipped, Assert.Single(result.Items).Status);
        var saved = await host.ReadAsync(request.Id);
        Assert.Equal("并发更新的备注", saved.Remarks);
        Assert.Equal(RequestStatus.Pending, saved.Status);
        Assert.Equal(1, host.Interceptor.SaveAttempts);
    }

    [Fact]
    public async Task Persistent_competition_stops_after_three_attempts()
    {
        await using var host = new TestHost();
        var request = Request(Now.AddDays(-10));
        await host.SeedAsync(request);
        async Task CompeteAsync()
        {
            await host.ChangeAsync(request.Id, item => item.Remarks = "持续修改");
            host.Interceptor.BeforeSave = CompeteAsync;
        }
        host.Interceptor.BeforeSave = CompeteAsync;

        var result = await host.Review.UpdateAsync(request.Id, request.Version, RequestStatus.Approved, null, null);

        Assert.Equal(CertificationReviewStatus.Conflict, result.Status);
        Assert.Equal(3, host.Interceptor.SaveAttempts);
        Assert.Equal(RequestStatus.Pending, (await host.ReadAsync(request.Id)).Status);
    }

    [Fact]
    public async Task Failed_request_does_not_prevent_later_requests_and_cancel_preserves_partial_results()
    {
        await using var host = new TestHost();
        var failed = Request(Now.AddDays(-12));
        var completed = Request(Now.AddDays(-11));
        var remaining = Request(Now.AddDays(-10));
        await host.SeedAsync(failed, completed, remaining);
        using var cancellation = new CancellationTokenSource();
        host.Interceptor.BeforeSave = () =>
        {
            host.Interceptor.BeforeSave = () => { cancellation.Cancel(); return Task.CompletedTask; };
            throw new InvalidOperationException("simulated storage failure");
        };

        var result = await host.Cleanup.RejectExpiredAsync(Now.AddDays(-7), false, cancellation.Token);

        Assert.True(result.IsCanceled);
        Assert.Equal([CertificationRequestCleanupStatus.Failed, CertificationRequestCleanupStatus.Rejected,
            CertificationRequestCleanupStatus.WouldReject], result.Items.Select(item => item.Status));
        Assert.Equal(RequestStatus.Pending, (await host.ReadAsync(failed.Id)).Status);
        Assert.Equal(RequestStatus.Rejected, (await host.ReadAsync(completed.Id)).Status);
        Assert.Equal(RequestStatus.Pending, (await host.ReadAsync(remaining.Id)).Status);
    }

    [Fact]
    public async Task Review_does_not_save_callers_unrelated_changes_or_use_its_stale_tracked_copy()
    {
        await using var host = new TestHost();
        var request = Request(Now.AddDays(-10));
        await host.SeedAsync(request);
        var caller = host.Services.GetRequiredService<ApplicationDbContext>();
        var tracked = await caller.ProfessionalCertificationRequests.SingleAsync();
        tracked.InstitutionName = "未保存的修改";
        await host.ChangeAsync(request.Id, item => item.Remarks = "其他上下文修改");

        var result = await host.Review.UpdateAsync(request.Id, request.Version, RequestStatus.Rejected, null, null);

        Assert.Equal(CertificationReviewStatus.Updated, result.Status);
        Assert.Equal(request.InstitutionName, (await host.ReadAsync(request.Id)).InstitutionName);
        Assert.Equal("未保存的修改", tracked.InstitutionName);
    }

    [Fact]
    public async Task Worker_uses_persisted_configuration_and_respects_interval_and_runtime_disable()
    {
        await using var host = new TestHost();
        var first = Request(Now.AddDays(-10));
        await host.SeedAsync(first);
        await host.Worker.ScanIfDueAsync(default);
        Assert.Equal(RequestStatus.Pending, (await host.ReadAsync(first.Id)).Status);
        await host.SaveSettingsAsync(new() { AutoRejectEnabled = true, PendingTimeoutDays = 7 });
        await host.Worker.ScanIfDueAsync(default);
        Assert.Equal(RequestStatus.Pending, (await host.ReadAsync(first.Id)).Status);
        await host.SaveSettingsAsync(EnabledSettings());
        await host.Worker.ScanIfDueAsync(default);
        Assert.Equal(RequestStatus.Rejected, (await host.ReadAsync(first.Id)).Status);

        var next = Request(Now.AddDays(-10));
        await host.SeedAsync(next);
        host.Clock.UtcNow = Now.AddMinutes(59);
        await host.Worker.ScanIfDueAsync(default);
        Assert.Equal(RequestStatus.Pending, (await host.ReadAsync(next.Id)).Status);
        // 直接修改数据库模拟其他实例关闭开关；本实例 Options 缓存仍是启用状态。
        await using (var db = host.CreateContext())
        {
            var setting = await db.ApplicationSettings.SingleAsync();
            setting.ValueJson = "{\"autoRejectEnabled\":false}";
            setting.Version = Guid.NewGuid();
            await db.SaveChangesAsync();
        }
        Assert.True(host.Services.GetRequiredService<IOptionsMonitor<CertificationRequestCleanupOptions>>().CurrentValue.AutoRejectEnabled);
        host.Clock.UtcNow = Now.AddHours(1);
        await host.Worker.ScanIfDueAsync(default);
        Assert.Equal(RequestStatus.Pending, (await host.ReadAsync(next.Id)).Status);
        await host.SaveSettingsAsync(EnabledSettings());
        await host.Worker.ScanIfDueAsync(default);
        Assert.Equal(RequestStatus.Rejected, (await host.ReadAsync(next.Id)).Status);
    }

    [Fact]
    public async Task Configuration_change_stops_sweep_between_requests()
    {
        await using var host = new TestHost();
        var first = Request(Now.AddDays(-11));
        var second = Request(Now.AddDays(-10));
        await host.SeedAsync(first, second);
        var settings = await host.SaveSettingsAsync(EnabledSettings());
        host.Interceptor.BeforeSave = async () => await host.SaveSettingsAsync(new());

        var result = await host.Cleanup.RejectConfiguredExpiredAsync(Now.AddDays(-7), settings.Version!.Value, default);

        Assert.True(result.ConfigurationChanged);
        Assert.Equal([CertificationRequestCleanupStatus.Rejected, CertificationRequestCleanupStatus.WouldReject],
            result.Items.Select(item => item.Status));
        Assert.Equal(RequestStatus.Pending, (await host.ReadAsync(second.Id)).Status);
    }

    [Fact]
    public async Task Invalid_configuration_prevents_automatic_processing()
    {
        await using var host = new TestHost();
        var request = Request(Now.AddDays(-10));
        await host.SeedAsync(request);
        await host.SaveSettingsAsync(EnabledSettings());
        await using (var db = host.CreateContext())
        {
            var settings = await db.ApplicationSettings.SingleAsync();
            settings.ValueJson = "{invalid";
            await db.SaveChangesAsync();
        }

        await Assert.ThrowsAnyAsync<Exception>(() => host.Worker.ScanIfDueAsync(default));

        Assert.Equal(RequestStatus.Pending, (await host.ReadAsync(request.Id)).Status);
    }

    [Fact]
    public async Task Invalid_cutoffs_empty_versions_and_ambient_transactions_are_rejected()
    {
        await using var host = new TestHost();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => host.Cleanup.RejectExpiredAsync(Now.AddSeconds(1)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => host.Cleanup.RejectExpiredAsync(DateTimeOffset.MinValue));
        var result = await host.Review.UpdateAsync(Guid.NewGuid(), Guid.Empty, RequestStatus.Rejected, null, null);
        Assert.Equal(CertificationReviewStatus.InvalidVersion, result.Status);
        using var transaction = new System.Transactions.TransactionScope(System.Transactions.TransactionScopeAsyncFlowOption.Enabled);
        await Assert.ThrowsAsync<InvalidOperationException>(() => host.Review.UpdateAsync(
            Guid.NewGuid(), Guid.NewGuid(), RequestStatus.Approved, null, null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => host.Cleanup.RejectExpiredAsync(Now));
    }

    private static CertificationRequestCleanupOptions EnabledSettings() => new()
    { AutoRejectEnabled = true, PendingTimeoutDays = 7, SweepIntervalHours = 1 };

    private static ProfessionalCertificationRequest Request(DateTimeOffset submitted, RequestStatus status = RequestStatus.Pending) => new()
    {
        Id = Guid.NewGuid(), UserId = Guid.NewGuid().ToString(), RequestTime = submitted.UtcDateTime,
        IdentityType = "Physician", InstitutionName = "原始机构", Status = status, CertificateTicket = Guid.NewGuid()
    };

    /// <summary>只使用隔离的内存 SQLite 和临时目录，不访问站点配置或真实数据库。</summary>
    private sealed class TestHost : IAsyncDisposable
    {
        private readonly SqliteConnection connection = new($"Data Source={Guid.NewGuid():N};Mode=Memory;Cache=Shared");
        private readonly string contentRoot = Path.Combine(Path.GetTempPath(), "EzNutrition.Certification.Tests", Guid.NewGuid().ToString("N"));
        private readonly ServiceProvider provider;
        private readonly AsyncServiceScope scope;
        public BeforeSaveInterceptor Interceptor { get; } = new();
        public TestClock Clock { get; } = new();
        public IServiceProvider Services => scope.ServiceProvider;
        public CertificationReviewService Review => Services.GetRequiredService<CertificationReviewService>();
        public CertificationRequestCleanupService Cleanup => Services.GetRequiredService<CertificationRequestCleanupService>();
        public CertificationRequestCleanupWorker Worker { get; }

        public TestHost()
        {
            connection.Open();
            using (var db = CreateContext()) db.Database.EnsureCreated();
            Directory.CreateDirectory(contentRoot);
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<TimeProvider>(Clock);
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection.ConnectionString).AddInterceptors(Interceptor));
            services.AddSingleton<IWebHostEnvironment>(new TestEnvironment(contentRoot));
            services.AddSingleton<CertificateFileStore>();
            services.AddScoped<CertificationReviewService>();
            services.AddScoped<CertificationRequestCleanupService>();
            services.AddSingleton<IValidateOptions<CertificationRequestCleanupOptions>, CertificationRequestCleanupOptionsValidator>();
            services.AddDatabaseSettings<CertificationRequestCleanupOptions>(CertificationRequestCleanupOptions.SectionName);
            provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
            scope = provider.CreateAsyncScope();
            Worker = new(provider.GetRequiredService<IServiceScopeFactory>(), Clock,
                NullLogger<CertificationRequestCleanupWorker>.Instance);
        }

        public ApplicationDbContext CreateContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection.ConnectionString).Options);

        public async Task SeedAsync(params ProfessionalCertificationRequest[] requests)
        {
            await using var db = CreateContext();
            db.ProfessionalCertificationRequests.AddRange(requests);
            await db.SaveChangesAsync();
        }

        public async Task<ProfessionalCertificationRequest> ReadAsync(Guid id)
        {
            await using var db = CreateContext();
            return await db.ProfessionalCertificationRequests.SingleAsync(item => item.Id == id);
        }

        public async Task ChangeAsync(Guid id, Action<ProfessionalCertificationRequest> change)
        {
            await using var db = CreateContext();
            var item = await db.ProfessionalCertificationRequests.SingleAsync(item => item.Id == id);
            change(item);
            item.Version = Guid.NewGuid();
            await db.SaveChangesAsync();
        }

        public async Task<DatabaseSettingsValue<CertificationRequestCleanupOptions>> SaveSettingsAsync(CertificationRequestCleanupOptions value)
        {
            var settings = Services.GetRequiredService<DatabaseSettings<CertificationRequestCleanupOptions>>();
            var current = await settings.GetAsync();
            return await settings.SaveAsync(value, current.Version);
        }

        public async ValueTask DisposeAsync()
        {
            Worker.Dispose();
            await scope.DisposeAsync();
            await provider.DisposeAsync();
            await connection.DisposeAsync();
            var fullPath = Path.GetFullPath(contentRoot);
            var parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "EzNutrition.Certification.Tests")) + Path.DirectorySeparatorChar;
            if (fullPath.StartsWith(parent, StringComparison.OrdinalIgnoreCase)) Directory.Delete(fullPath, recursive: true);
        }
    }

    /// <summary>在 SQL 保存之前制造独立竞争写入，验证数据库并发条件和有限重试。</summary>
    private sealed class BeforeSaveInterceptor : SaveChangesInterceptor
    {
        public Func<Task>? BeforeSave { get; set; }
        public int SaveAttempts { get; private set; }
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<ProfessionalCertificationRequest>().Any(entry => entry.State == EntityState.Modified))
            {
                SaveAttempts++;
                var action = BeforeSave;
                BeforeSave = null;
                if (action is not null) await action();
            }
            return result;
        }
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = Now;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class TestEnvironment(string contentRoot) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "CertificationTests";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = contentRoot;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
