using EzNutrition.Server.Data;
using EzNutrition.Server.Data.Entities;
using EzNutrition.Server.Extension;
using EzNutrition.Server.Services.Settings;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Transactions;

namespace EzNutrition.Server.Tests.Services.Settings;

public sealed class DatabaseSettingsTests
{
    [Fact]
    public async Task Missing_row_uses_disabled_defaults_without_creating_a_record()
    {
        await using var database = new TestDatabase();
        await using var provider = await database.CreateProviderAsync();
        await using var scope = provider.CreateAsyncScope();
        var current = await scope.ServiceProvider.GetRequiredService<DatabaseSettings<AccountCleanupOptions>>().GetAsync();

        Assert.Null(current.Version);
        Assert.Null(current.UpdatedAtUtc);
        Assert.Null(current.UpdatedByUserId);
        Assert.Equal(1, current.SchemaVersion);
        Assert.False(current.Value.UnsubmittedCertificationCleanupEnabled);
        Assert.False(current.Value.NonFormalAccountCleanupEnabled);
        Assert.False(current.Value.InactiveFormalAccountCleanupEnabled);
        Assert.Null(current.Value.CertificationSubmissionGraceDays);
        Assert.Null(current.Value.NonFormalAccountRetentionDays);
        Assert.Null(current.Value.FormalAccountInactivityDays);
        await using var context = database.CreateContext();
        Assert.Empty(await context.ApplicationSettings.ToArrayAsync());
    }

    [Fact]
    public async Task Options_cannot_be_read_before_database_initialization()
    {
        await using var database = new TestDatabase();
        await using var provider = await database.CreateProviderAsync(initialize: false);

        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IOptions<AccountCleanupOptions>>().Value);
    }

    [Fact]
    public async Task Save_persists_metadata_and_survives_a_new_service_provider()
    {
        await using var database = new TestDatabase();
        await using var provider = await database.CreateProviderAsync();
        var submitted = CreateOptions();
        var saved = await SaveAsync(provider, submitted, null, "admin-user");
        submitted.NonFormalAccountRetentionDays = 999;
        saved.Value.FormalAccountInactivityDays = 999;

        await using var restarted = await database.CreateProviderAsync();
        await using var scope = restarted.CreateAsyncScope();
        var loaded = await scope.ServiceProvider.GetRequiredService<DatabaseSettings<AccountCleanupOptions>>().GetAsync();

        Assert.NotNull(saved.Version);
        Assert.NotEqual(Guid.Empty, saved.Version);
        Assert.Equal(saved.Version, loaded.Version);
        Assert.Equal(database.Clock.GetUtcNow().UtcDateTime, loaded.UpdatedAtUtc);
        Assert.Equal(DateTimeKind.Utc, loaded.UpdatedAtUtc!.Value.Kind);
        Assert.Equal("admin-user", loaded.UpdatedByUserId);
        Assert.Equal(30, loaded.Value.NonFormalAccountRetentionDays);
        Assert.Equal(365, loaded.Value.FormalAccountInactivityDays);
        loaded.Value.CertificationSubmissionGraceDays = 999;
        Assert.Equal(7, restarted.GetRequiredService<IOptionsMonitor<AccountCleanupOptions>>()
            .CurrentValue.CertificationSubmissionGraceDays);
    }

    [Fact]
    public async Task Save_updates_monitor_and_new_scopes_while_preserving_standard_options_lifetimes()
    {
        await using var database = new TestDatabase();
        await using var provider = await database.CreateProviderAsync();
        var options = provider.GetRequiredService<IOptions<AccountCleanupOptions>>().Value;
        var monitor = provider.GetRequiredService<IOptionsMonitor<AccountCleanupOptions>>();
        await using var originalScope = provider.CreateAsyncScope();
        var originalSnapshot = originalScope.ServiceProvider.GetRequiredService<IOptionsSnapshot<AccountCleanupOptions>>().Value;
        List<AccountCleanupOptions> notifications = [];
        using var subscription = monitor.OnChange((value, _) => notifications.Add(value));

        await SaveAsync(provider, CreateOptions(), null);

        Assert.False(options.NonFormalAccountCleanupEnabled);
        Assert.False(originalSnapshot.NonFormalAccountCleanupEnabled);
        Assert.True(monitor.CurrentValue.NonFormalAccountCleanupEnabled);
        Assert.True(Assert.Single(notifications).NonFormalAccountCleanupEnabled);
        await using var newScope = provider.CreateAsyncScope();
        Assert.True(newScope.ServiceProvider.GetRequiredService<IOptionsSnapshot<AccountCleanupOptions>>()
            .Value.NonFormalAccountCleanupEnabled);
        Assert.Same(originalSnapshot, originalScope.ServiceProvider
            .GetRequiredService<IOptionsSnapshot<AccountCleanupOptions>>().Value);
    }

    [Fact]
    public async Task Stale_versions_cannot_overwrite_an_existing_record()
    {
        await using var database = new TestDatabase();
        await using var provider = await database.CreateProviderAsync();
        var initial = await SaveAsync(provider, CreateOptions(), null);
        var updated = await SaveAsync(provider, new() { NonFormalAccountRetentionDays = 48 }, initial.Version);

        await Assert.ThrowsAsync<DatabaseSettingsConcurrencyException>(() =>
            SaveAsync(provider, CreateOptions(), initial.Version));
        await Assert.ThrowsAsync<DatabaseSettingsConcurrencyException>(() =>
            SaveAsync(provider, CreateOptions(), null));

        await using var context = database.CreateContext();
        Assert.Equal(updated.Version, (await context.ApplicationSettings.SingleAsync()).Version);
        Assert.Equal(48, provider.GetRequiredService<IOptionsMonitor<AccountCleanupOptions>>().CurrentValue.NonFormalAccountRetentionDays);
    }

    [Fact]
    public async Task Database_concurrency_token_prevents_an_update_lost_between_read_and_commit()
    {
        await using var database = new TestDatabase();
        var interceptor = new BeforeSaveInterceptor();
        await using var provider = await database.CreateProviderAsync(interceptor: interceptor);
        var initial = await SaveAsync(provider, CreateOptions(), null);
        await using var otherProvider = await database.CreateProviderAsync();
        DatabaseSettingsValue<AccountCleanupOptions>? winner = null;
        interceptor.BeforeSave = async () =>
        {
            winner = await SaveAsync(otherProvider, new() { NonFormalAccountRetentionDays = 48 }, initial.Version);
        };

        await Assert.ThrowsAsync<DatabaseSettingsConcurrencyException>(() =>
            SaveAsync(provider, new() { NonFormalAccountRetentionDays = 72 }, initial.Version));

        await using var context = database.CreateContext();
        Assert.Equal(winner!.Version, (await context.ApplicationSettings.SingleAsync()).Version);
        Assert.Equal(30, provider.GetRequiredService<IOptionsMonitor<AccountCleanupOptions>>().CurrentValue.NonFormalAccountRetentionDays);
        await ReloadAsync(provider);
        Assert.Equal(48, provider.GetRequiredService<IOptionsMonitor<AccountCleanupOptions>>().CurrentValue.NonFormalAccountRetentionDays);
    }

    [Fact]
    public async Task Concurrent_first_insert_is_reported_as_a_version_conflict()
    {
        await using var database = new TestDatabase();
        var interceptor = new BeforeSaveInterceptor();
        await using var provider = await database.CreateProviderAsync(interceptor: interceptor);
        await using var otherProvider = await database.CreateProviderAsync();
        interceptor.BeforeSave = () => SaveAsync(otherProvider, new() { NonFormalAccountRetentionDays = 48 }, null);

        await Assert.ThrowsAsync<DatabaseSettingsConcurrencyException>(() =>
            SaveAsync(provider, CreateOptions(), null));

        await using var context = database.CreateContext();
        Assert.Single(await context.ApplicationSettings.ToArrayAsync());
        Assert.False(provider.GetRequiredService<IOptionsMonitor<AccountCleanupOptions>>().CurrentValue.NonFormalAccountCleanupEnabled);
    }

    [Fact]
    public async Task Failed_commit_never_publishes_and_can_be_retried()
    {
        await using var database = new TestDatabase();
        var interceptor = new BeforeSaveInterceptor();
        await using var provider = await database.CreateProviderAsync(interceptor: interceptor);
        var monitor = provider.GetRequiredService<IOptionsMonitor<AccountCleanupOptions>>();
        var notifications = 0;
        using var subscription = monitor.OnChange((_, _) => notifications++);
        interceptor.BeforeSave = () => throw new DbUpdateException("simulated write failure");

        await Assert.ThrowsAsync<DbUpdateException>(() => SaveAsync(provider, CreateOptions(), null));

        Assert.Equal(0, notifications);
        Assert.False(monitor.CurrentValue.NonFormalAccountCleanupEnabled);
        await using var context = database.CreateContext();
        Assert.Empty(await context.ApplicationSettings.ToArrayAsync());
        await SaveAsync(provider, CreateOptions(), null);
        Assert.Equal(1, notifications);
    }

    [Fact]
    public async Task Invalid_candidate_is_rejected_before_persistence_or_notification()
    {
        await using var database = new TestDatabase();
        await using var provider = await database.CreateProviderAsync();
        var monitor = provider.GetRequiredService<IOptionsMonitor<AccountCleanupOptions>>();
        var notifications = 0;
        using var subscription = monitor.OnChange((_, _) => notifications++);

        await Assert.ThrowsAsync<OptionsValidationException>(() => SaveAsync(provider,
            new() { NonFormalAccountCleanupEnabled = true }, null));

        Assert.Equal(0, notifications);
        Assert.False(monitor.CurrentValue.NonFormalAccountCleanupEnabled);
        await using var context = database.CreateContext();
        Assert.Empty(await context.ApplicationSettings.ToArrayAsync());
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{\"unknownField\":true}")]
    [InlineData("{\"nonFormalAccountRetentionDays\":\"30\"}")]
    public async Task Malformed_stored_document_preserves_last_good_options_but_fresh_reads_fail(string json)
    {
        await using var database = new TestDatabase();
        await using var provider = await database.CreateProviderAsync();
        await SaveAsync(provider, CreateOptions(), null);
        var monitor = provider.GetRequiredService<IOptionsMonitor<AccountCleanupOptions>>();
        var notifications = 0;
        using var subscription = monitor.OnChange((_, _) => notifications++);
        await using (var context = database.CreateContext())
        {
            var entity = await context.ApplicationSettings.SingleAsync();
            entity.ValueJson = json;
            entity.Version = Guid.NewGuid();
            await context.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<InvalidDataException>(() => ReloadAsync(provider));
        await using var scope = provider.CreateAsyncScope();
        await Assert.ThrowsAsync<InvalidDataException>(() => scope.ServiceProvider
            .GetRequiredService<DatabaseSettings<AccountCleanupOptions>>().GetAsync());
        Assert.Equal(0, notifications);
        Assert.Equal(30, monitor.CurrentValue.NonFormalAccountRetentionDays);
    }

    [Fact]
    public async Task Unsupported_schema_cannot_be_loaded_or_overwritten()
    {
        await using var database = new TestDatabase();
        await using var provider = await database.CreateProviderAsync();
        var saved = await SaveAsync(provider, CreateOptions(), null);
        await using (var context = database.CreateContext())
        {
            var entity = await context.ApplicationSettings.SingleAsync();
            entity.SchemaVersion = 2;
            await context.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<InvalidDataException>(() => ReloadAsync(provider));
        await Assert.ThrowsAsync<InvalidDataException>(() => SaveAsync(provider, CreateOptions(), saved.Version));
        Assert.Equal(30, provider.GetRequiredService<IOptionsMonitor<AccountCleanupOptions>>().CurrentValue.NonFormalAccountRetentionDays);
    }

    [Fact]
    public async Task Invalid_persisted_values_fail_startup_instead_of_falling_back_to_defaults()
    {
        await using var database = new TestDatabase();
        await using var provider = await database.CreateProviderAsync();
        await using (var context = database.CreateContext())
        {
            context.ApplicationSettings.Add(new()
            {
                Key = AccountCleanupOptions.SectionName,
                ValueJson = "{\"nonFormalAccountRetentionDays\":0}",
                Version = Guid.NewGuid(),
                UpdatedAtUtc = database.Clock.GetUtcNow().UtcDateTime
            });
            await context.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<OptionsValidationException>(() => provider.LoadDatabaseSettingsAsync());
    }

    [Fact]
    public async Task Save_does_not_commit_unrelated_changes_in_the_callers_context()
    {
        await using var database = new TestDatabase();
        await using var provider = await database.CreateProviderAsync();
        await using var scope = provider.CreateAsyncScope();
        var callersContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        callersContext.Users.Add(new ApplicationUser { Id = "unsaved-user", UserName = "unsaved" });

        await scope.ServiceProvider.GetRequiredService<DatabaseSettings<AccountCleanupOptions>>().SaveAsync(CreateOptions(), null);

        await using var verification = database.CreateContext();
        Assert.Empty(await verification.Users.ToArrayAsync());
        Assert.Single(await verification.ApplicationSettings.ToArrayAsync());
        Assert.Single(callersContext.ChangeTracker.Entries<ApplicationUser>());
    }

    [Fact]
    public async Task Publishing_operations_reject_ambient_transactions_that_could_delay_commit_past_notification()
    {
        await using var database = new TestDatabase();
        await using var provider = await database.CreateProviderAsync();
        using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => SaveAsync(provider, CreateOptions(), null));
            await Assert.ThrowsAsync<InvalidOperationException>(() => ReloadAsync(provider));
        }
        await using var context = database.CreateContext();
        Assert.Empty(await context.ApplicationSettings.ToArrayAsync());
    }

    [Fact]
    public async Task Throwing_subscriber_does_not_turn_a_committed_save_into_a_failure()
    {
        await using var database = new TestDatabase();
        await using var provider = await database.CreateProviderAsync();
        var monitor = provider.GetRequiredService<IOptionsMonitor<AccountCleanupOptions>>();
        using var subscription = monitor.OnChange((_, _) => throw new InvalidOperationException("subscriber failure"));

        var saved = await SaveAsync(provider, CreateOptions(), null);
        var next = await SaveAsync(provider, new() { NonFormalAccountRetentionDays = 48 }, saved.Version);

        Assert.NotEqual(saved.Version, next.Version);
        Assert.Equal(48, monitor.CurrentValue.NonFormalAccountRetentionDays);
    }

    [Fact]
    public async Task Reload_refreshes_other_instances_once_and_keeps_configuration_groups_independent()
    {
        await using var database = new TestDatabase();
        await using var provider = await database.CreateProviderAsync();
        await using var otherProvider = await database.CreateProviderAsync();
        var monitor = provider.GetRequiredService<IOptionsMonitor<AccountCleanupOptions>>();
        var otherGroup = provider.GetRequiredService<IOptionsMonitor<OtherOptions>>();
        var cleanupNotifications = 0;
        var otherNotifications = 0;
        using var cleanupSubscription = monitor.OnChange((_, _) => cleanupNotifications++);
        using var otherSubscription = otherGroup.OnChange((_, _) => otherNotifications++);

        await SaveAsync(otherProvider, CreateOptions(), null);
        Assert.False(monitor.CurrentValue.NonFormalAccountCleanupEnabled);
        await ReloadAsync(provider);
        await ReloadAsync(provider);
        Assert.True(monitor.CurrentValue.NonFormalAccountCleanupEnabled);
        Assert.Equal(1, cleanupNotifications);
        Assert.Equal(0, otherNotifications);
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DatabaseSettings<OtherOptions>>()
            .SaveAsync(new() { Message = "updated" }, null);
        Assert.Equal("updated", otherGroup.CurrentValue.Message);
        Assert.Equal(1, otherNotifications);
        Assert.Equal(1, cleanupNotifications);
        Assert.IsType<DatabaseSettingsReloadWorker>(Assert.Single(provider.GetServices<IHostedService>()));
    }

    [Fact]
    public async Task Reload_worker_observes_cross_instance_writes_on_its_timer()
    {
        await using var database = new TestDatabase();
        await using var provider = await database.CreateProviderAsync();
        await using var otherProvider = await database.CreateProviderAsync();
        var monitor = provider.GetRequiredService<IOptionsMonitor<AccountCleanupOptions>>();
        var notification = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = monitor.OnChange((_, _) => notification.TrySetResult());
        var worker = Assert.Single(provider.GetServices<IHostedService>());
        await worker.StartAsync(CancellationToken.None);
        try
        {
            var timer = await database.Clock.TimerCreated.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await SaveAsync(otherProvider, CreateOptions(), null);
            timer.Tick();
            await notification.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(monitor.CurrentValue.NonFormalAccountCleanupEnabled);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Non_account_cleanup_groups_persist_and_notify_independently()
    {
        await using var database = new TestDatabase();
        await using var provider = await database.CreateProviderAsync();
        var accountMonitor = provider.GetRequiredService<IOptionsMonitor<AccountCleanupOptions>>();
        var certificationMonitor = provider.GetRequiredService<IOptionsMonitor<CertificationRequestCleanupOptions>>();
        var auditMonitor = provider.GetRequiredService<IOptionsMonitor<LlmAuditCleanupOptions>>();
        Assert.False(certificationMonitor.CurrentValue.AutoRejectEnabled);
        Assert.Null(certificationMonitor.CurrentValue.PendingTimeoutDays);
        Assert.False(auditMonitor.CurrentValue.Enabled);
        Assert.Null(auditMonitor.CurrentValue.RetentionDays);
        var accountNotifications = 0;
        var certificationNotifications = 0;
        var auditNotifications = 0;
        using var accountSubscription = accountMonitor.OnChange((_, _) => accountNotifications++);
        using var certificationSubscription = certificationMonitor.OnChange((_, _) => certificationNotifications++);
        using var auditSubscription = auditMonitor.OnChange((_, _) => auditNotifications++);
        await using var scope = provider.CreateAsyncScope();
        var certificationSettings = scope.ServiceProvider.GetRequiredService<DatabaseSettings<CertificationRequestCleanupOptions>>();
        var auditSettings = scope.ServiceProvider.GetRequiredService<DatabaseSettings<LlmAuditCleanupOptions>>();
        var certification = await certificationSettings.GetAsync();
        var audit = await auditSettings.GetAsync();
        Assert.Null(certification.Version);
        Assert.Null(audit.Version);
        certification.Value.AutoRejectEnabled = true;
        certification.Value.PendingTimeoutDays = 14;

        var savedCertification = await certificationSettings.SaveAsync(certification.Value, certification.Version);

        Assert.Equal(1, certificationNotifications);
        Assert.Equal(0, auditNotifications);
        Assert.Equal(14, certificationMonitor.CurrentValue.PendingTimeoutDays);
        audit.Value.Enabled = true;
        audit.Value.RetentionDays = 90;

        var savedAudit = await auditSettings.SaveAsync(audit.Value, audit.Version);

        Assert.Equal(1, certificationNotifications);
        Assert.Equal(1, auditNotifications);
        Assert.Equal(0, accountNotifications);
        Assert.Equal(90, auditMonitor.CurrentValue.RetentionDays);
        await using var restarted = await database.CreateProviderAsync();
        await using var restartedScope = restarted.CreateAsyncScope();
        var loadedCertification = await restartedScope.ServiceProvider
            .GetRequiredService<DatabaseSettings<CertificationRequestCleanupOptions>>().GetAsync();
        var loadedAudit = await restartedScope.ServiceProvider
            .GetRequiredService<DatabaseSettings<LlmAuditCleanupOptions>>().GetAsync();
        Assert.Equal(savedCertification.Version, loadedCertification.Version);
        Assert.Equal(savedAudit.Version, loadedAudit.Version);
        var restartedCertification = restarted.GetRequiredService<IOptionsMonitor<CertificationRequestCleanupOptions>>().CurrentValue;
        var restartedAudit = restarted.GetRequiredService<IOptionsMonitor<LlmAuditCleanupOptions>>().CurrentValue;
        Assert.True(restartedCertification.AutoRejectEnabled);
        Assert.Equal(14, restartedCertification.PendingTimeoutDays);
        Assert.True(restartedAudit.Enabled);
        Assert.Equal(90, restartedAudit.RetentionDays);
        await using var context = database.CreateContext();
        Assert.Equal(
            [CertificationRequestCleanupOptions.SectionName, LlmAuditCleanupOptions.SectionName],
            await context.ApplicationSettings.OrderBy(setting => setting.Key).Select(setting => setting.Key).ToArrayAsync());
    }

    [Fact]
    public async Task Non_account_cleanup_groups_validate_before_persistence()
    {
        await using var database = new TestDatabase();
        await using var provider = await database.CreateProviderAsync();
        await using var scope = provider.CreateAsyncScope();

        await Assert.ThrowsAsync<OptionsValidationException>(() => scope.ServiceProvider
            .GetRequiredService<DatabaseSettings<CertificationRequestCleanupOptions>>()
            .SaveAsync(new() { AutoRejectEnabled = true }, null));
        await Assert.ThrowsAsync<OptionsValidationException>(() => scope.ServiceProvider
            .GetRequiredService<DatabaseSettings<LlmAuditCleanupOptions>>()
            .SaveAsync(new() { Enabled = true }, null));

        await using var context = database.CreateContext();
        Assert.Empty(await context.ApplicationSettings.ToArrayAsync());
    }

    /// <summary>示例天数仅用于测试，不代表产品默认值。</summary>
    private static AccountCleanupOptions CreateOptions() => new()
    {
        UnsubmittedCertificationCleanupEnabled = true,
        CertificationSubmissionGraceDays = 7,
        NonFormalAccountCleanupEnabled = true,
        NonFormalAccountRetentionDays = 30,
        InactiveFormalAccountCleanupEnabled = true,
        FormalAccountInactivityDays = 365
    };

    private static async Task<DatabaseSettingsValue<AccountCleanupOptions>> SaveAsync(
        IServiceProvider provider, AccountCleanupOptions value, Guid? expectedVersion, string? actor = null)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<DatabaseSettings<AccountCleanupOptions>>()
            .SaveAsync(value, expectedVersion, actor);
    }

    private static async Task ReloadAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DatabaseSettings<AccountCleanupOptions>>().ReloadAsync();
    }

    /// <summary>第二个配置组用于验证扩展注册与变更通知相互独立。</summary>
    public sealed class OtherOptions
    {
        public string Message { get; set; } = "initial";
    }

    /// <summary>仅在内存 SQLite 中运行持久化测试，不访问站点数据库。</summary>
    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");
        public ManualTimeProvider Clock { get; } = new();

        public TestDatabase()
        {
            connection.Open();
            using var context = CreateContext();
            context.Database.EnsureCreated();
        }

        public ApplicationDbContext CreateContext() => new(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);

        public async Task<ServiceProvider> CreateProviderAsync(bool initialize = true, IInterceptor? interceptor = null)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<TimeProvider>(Clock);
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlite(connection);
                if (interceptor is not null)
                {
                    options.AddInterceptors(interceptor);
                }
            });
            services.AddSingleton<IValidateOptions<AccountCleanupOptions>, AccountCleanupOptionsValidator>();
            services.AddDatabaseSettings<AccountCleanupOptions>(AccountCleanupOptions.SectionName);
            services.AddSingleton<IValidateOptions<CertificationRequestCleanupOptions>, CertificationRequestCleanupOptionsValidator>();
            services.AddDatabaseSettings<CertificationRequestCleanupOptions>(CertificationRequestCleanupOptions.SectionName);
            services.AddSingleton<IValidateOptions<LlmAuditCleanupOptions>, LlmAuditCleanupOptionsValidator>();
            services.AddDatabaseSettings<LlmAuditCleanupOptions>(LlmAuditCleanupOptions.SectionName);
            services.AddDatabaseSettings<OtherOptions>("Other");
            var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
            try
            {
                if (initialize)
                {
                    await provider.LoadDatabaseSettingsAsync();
                }
                return provider;
            }
            catch
            {
                await provider.DisposeAsync();
                throw;
            }
        }

        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }

    /// <summary>在真正提交前插入竞争写入或失败，验证数据库层保护而非仅验证本地版本比较。</summary>
    private sealed class BeforeSaveInterceptor : SaveChangesInterceptor
    {
        public Func<Task>? BeforeSave { get; set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var action = BeforeSave;
            BeforeSave = null;
            if (action is not null)
            {
                await action();
            }
            return result;
        }
    }

    /// <summary>固定审计时钟，并允许测试触发后台重载而无需真实等待三十秒。</summary>
    private sealed class ManualTimeProvider : TimeProvider
    {
        public TaskCompletionSource<ManualTimer> TimerCreated { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override DateTimeOffset GetUtcNow() => new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(callback, state);
            TimerCreated.TrySetResult(timer);
            return timer;
        }
    }

    private sealed class ManualTimer(TimerCallback callback, object? state) : ITimer
    {
        public void Tick() => callback(state);
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
