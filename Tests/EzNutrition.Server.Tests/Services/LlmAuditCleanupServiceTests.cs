using EzNutrition.Server.Data;
using EzNutrition.Server.Data.Entities;
using EzNutrition.Server.Extension;
using EzNutrition.Server.Services.Maintenance;
using EzNutrition.Server.Services.Settings;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EzNutrition.Server.Tests.Services;

/// <summary>验证 LLM 审计记录按请求时间清理及数据库配置调度。</summary>
public sealed class LlmAuditCleanupServiceTests
{
    [Fact]
    public async Task Preview_and_execute_use_the_same_strict_request_time_boundary()
    {
        await using var host = await TestHost.CreateAsync();
        var cutoff = TestHost.Now.AddDays(-30);
        await host.AddAuditAsync("expired", cutoff.AddTicks(-1));
        await host.AddAuditAsync("boundary", cutoff);
        await host.AddAuditAsync("fresh", cutoff.AddTicks(1));
        await host.AddAuditAsync("unknown", DateTimeOffset.MinValue);

        var preview = await host.Service.DeleteExpiredAsync(cutoff);

        Assert.True(preview.DryRun);
        Assert.Equal(1, preview.MatchedRecords);
        Assert.Equal(0, preview.DeletedRecords);
        Assert.Equal(4, await host.CountAuditsAsync());

        var executed = await host.Service.DeleteExpiredAsync(cutoff, dryRun: false);
        Assert.False(executed.DryRun);
        Assert.Equal(1, executed.MatchedRecords);
        Assert.Equal(1, executed.DeletedRecords);
        Assert.Equal(["boundary", "fresh", "unknown"], await host.ReadAuditPromptsAsync());
    }

    [Fact]
    public async Task Configured_execution_rejects_a_stale_version_without_deleting_records()
    {
        await using var host = await TestHost.CreateAsync();
        var staleVersion = await host.SaveSettingsAsync(new()
        {
            Enabled = true,
            RetentionDays = 30,
            SweepIntervalHours = 24
        });
        await host.SaveSettingsAsync(new()
        {
            Enabled = true,
            RetentionDays = 60,
            SweepIntervalHours = 24
        }, staleVersion);
        await host.AddAuditAsync("retained", TestHost.Now.AddDays(-90));

        var result = await host.Service.DeleteConfiguredExpiredAsync(
            TestHost.Now.AddDays(-30), staleVersion);

        Assert.True(result.ConfigurationChanged);
        Assert.Equal(0, result.DeletedRecords);
        Assert.Equal(1, await host.CountAuditsAsync());
    }

    [Fact]
    public async Task Worker_respects_retention_interval_disable_and_reenable()
    {
        await using var host = await TestHost.CreateAsync();
        var version = await host.SaveSettingsAsync(new()
        {
            Enabled = true,
            RetentionDays = 7,
            SweepIntervalHours = 24
        });
        await host.AddAuditAsync("expired", TestHost.Now.AddDays(-8));
        await host.AddAuditAsync("fresh", TestHost.Now.AddDays(-6));

        await host.Worker.ScanIfDueAsync(default);

        Assert.Equal(["fresh"], await host.ReadAuditPromptsAsync());
        await host.AddAuditAsync("waiting", TestHost.Now.AddDays(-8));
        await host.Worker.ScanIfDueAsync(default);
        Assert.Equal(["fresh", "waiting"], await host.ReadAuditPromptsAsync());

        version = await host.SaveSettingsAsync(new() { SweepIntervalHours = 24 }, version);
        await host.Worker.ScanIfDueAsync(default);
        version = await host.SaveSettingsAsync(new()
        {
            Enabled = true,
            RetentionDays = 7,
            SweepIntervalHours = 24
        }, version);
        await host.Worker.ScanIfDueAsync(default);

        Assert.Equal(["fresh"], await host.ReadAuditPromptsAsync());
    }

    [Fact]
    public async Task Invalid_cutoffs_and_ambient_transactions_are_rejected_before_work()
    {
        await using var host = await TestHost.CreateAsync();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            host.Service.DeleteExpiredAsync(TestHost.Now.AddTicks(1)));
        using var transaction = new System.Transactions.TransactionScope(
            System.Transactions.TransactionScopeAsyncFlowOption.Enabled);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            host.Service.DeleteExpiredAsync(TestHost.Now.AddDays(-1), dryRun: false));
    }

    /// <summary>使用共享内存 SQLite 验证真实批量删除，不访问站点数据库。</summary>
    private sealed class TestHost : IAsyncDisposable
    {
        internal static readonly DateTimeOffset Now =
            new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

        private readonly SqliteConnection connection;
        private readonly ServiceProvider services;
        private readonly AsyncServiceScope scope;

        private TestHost(
            SqliteConnection connection,
            ServiceProvider services,
            AsyncServiceScope scope)
        {
            this.connection = connection;
            this.services = services;
            this.scope = scope;
            Service = scope.ServiceProvider.GetRequiredService<LlmAuditCleanupService>();
            Worker = new LlmAuditCleanupWorker(
                services.GetRequiredService<IServiceScopeFactory>(),
                services.GetRequiredService<TimeProvider>(),
                NullLogger<LlmAuditCleanupWorker>.Instance);
        }

        public LlmAuditCleanupService Service { get; }

        public LlmAuditCleanupWorker Worker { get; }

        public static async Task<TestHost> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging();
            serviceCollection.AddSingleton<TimeProvider>(new FixedTimeProvider());
            serviceCollection.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
            serviceCollection.AddSingleton<IValidateOptions<LlmAuditCleanupOptions>, LlmAuditCleanupOptionsValidator>();
            serviceCollection.AddDatabaseSettings<LlmAuditCleanupOptions>(LlmAuditCleanupOptions.SectionName);
            serviceCollection.AddScoped<LlmAuditCleanupService>();
            var services = serviceCollection.BuildServiceProvider(
                new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
            try
            {
                await using (var initializationScope = services.CreateAsyncScope())
                {
                    await initializationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                        .Database.EnsureCreatedAsync();
                }
                await services.LoadDatabaseSettingsAsync();
                return new TestHost(connection, services, services.CreateAsyncScope());
            }
            catch
            {
                await services.DisposeAsync();
                await connection.DisposeAsync();
                throw;
            }
        }

        public async Task<Guid> SaveSettingsAsync(
            LlmAuditCleanupOptions value,
            Guid? expectedVersion = null)
        {
            var saved = await scope.ServiceProvider.GetRequiredService<DatabaseSettings<LlmAuditCleanupOptions>>()
                .SaveAsync(value, expectedVersion);
            return Assert.IsType<Guid>(saved.Version);
        }

        public async Task AddAuditAsync(string prompt, DateTimeOffset requestTime)
        {
            await using var context = CreateContext();
            context.PrescriptionGenerateRequests.Add(new PrescriptionGenerateRequest
            {
                Id = Guid.NewGuid(),
                UserId = "audit-user",
                Prompt = prompt,
                RequestTime = requestTime.UtcDateTime
            });
            await context.SaveChangesAsync();
        }

        public async Task<int> CountAuditsAsync()
        {
            await using var context = CreateContext();
            return await context.PrescriptionGenerateRequests.CountAsync();
        }

        public async Task<string[]> ReadAuditPromptsAsync()
        {
            await using var context = CreateContext();
            return await context.PrescriptionGenerateRequests
                .OrderBy(request => request.Prompt)
                .Select(request => request.Prompt)
                .ToArrayAsync();
        }

        private ApplicationDbContext CreateContext() => new(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);

        public async ValueTask DisposeAsync()
        {
            await scope.DisposeAsync();
            await services.DisposeAsync();
            await connection.DisposeAsync();
        }

        private sealed class FixedTimeProvider : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => Now;
        }
    }
}
