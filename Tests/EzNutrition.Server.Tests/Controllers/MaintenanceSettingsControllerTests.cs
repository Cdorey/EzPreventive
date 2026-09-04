using EzNutrition.Server.Controllers;
using EzNutrition.Server.Data;
using EzNutrition.Server.Extension;
using EzNutrition.Server.Services.Settings;
using EzNutrition.Shared.Data.DTO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace EzNutrition.Server.Tests.Controllers;

/// <summary>验证管理员维护配置接口的读取、保存、校验和并发边界。</summary>
public sealed class MaintenanceSettingsControllerTests
{
    [Fact]
    public async Task Get_returns_all_groups_with_disabled_defaults()
    {
        await using var host = await TestHost.CreateAsync();

        var action = await host.Controller.Get(CancellationToken.None);

        var result = Assert.IsType<OkObjectResult>(action.Result);
        var settings = Assert.IsType<MaintenanceSettingsDto>(result.Value);
        Assert.Equal(new TimeOnly(3, 30), settings.CleanupSchedule.Value.StartTime);
        Assert.False(settings.AccountCleanup.Value.NonFormalAccountCleanupEnabled);
        Assert.False(settings.CertificationRequestCleanup.Value.AutoRejectEnabled);
        Assert.False(settings.LlmAuditCleanup.Value.Enabled);
        Assert.Null(settings.CleanupSchedule.Version);
        Assert.Null(settings.AccountCleanup.Version);
        Assert.Null(settings.CertificationRequestCleanup.Version);
        Assert.Null(settings.LlmAuditCleanup.Version);
    }

    [Fact]
    public async Task Save_cleanup_schedule_persists_the_start_time()
    {
        await using var host = await TestHost.CreateAsync("admin-user-id");
        var request = new DatabaseSettingUpdateDto<CleanupScheduleSettingsDto>(new()
        {
            StartTime = new TimeOnly(4, 15)
        }, null);

        var action = await host.Controller.SaveCleanupSchedule(request, CancellationToken.None);

        var result = Assert.IsType<OkObjectResult>(action);
        var saved = Assert.IsType<DatabaseSettingDto<CleanupScheduleSettingsDto>>(result.Value);
        Assert.Equal(new TimeOnly(4, 15), saved.Value.StartTime);
        Assert.Equal("admin-user-id", saved.UpdatedByUserId);
        Assert.Equal(new TimeOnly(4, 15), (await host.CleanupSchedule.GetAsync()).Value.StartTime);
    }

    [Fact]
    public async Task Save_persists_the_complete_group_and_actor()
    {
        await using var host = await TestHost.CreateAsync("admin-user-id");
        var request = new DatabaseSettingUpdateDto<AccountCleanupSettingsDto>(new()
        {
            UnsubmittedCertificationCleanupEnabled = true,
            CertificationSubmissionGraceDays = 7,
            NonFormalAccountCleanupEnabled = true,
            NonFormalAccountRetentionDays = 30,
            InactiveFormalAccountCleanupEnabled = true,
            FormalAccountInactivityDays = 365
        }, null);

        var action = await host.Controller.SaveAccountCleanup(request, CancellationToken.None);

        var result = Assert.IsType<OkObjectResult>(action);
        var saved = Assert.IsType<DatabaseSettingDto<AccountCleanupSettingsDto>>(result.Value);
        Assert.NotNull(saved.Version);
        Assert.Equal("admin-user-id", saved.UpdatedByUserId);
        Assert.Equal(30, saved.Value.NonFormalAccountRetentionDays);

        var readAction = await host.Controller.Get(CancellationToken.None);
        var read = Assert.IsType<MaintenanceSettingsDto>(
            Assert.IsType<OkObjectResult>(readAction.Result).Value);
        Assert.Equal(saved.Version, read.AccountCleanup.Version);
        Assert.Equal(365, read.AccountCleanup.Value.FormalAccountInactivityDays);
    }

    [Fact]
    public async Task Stale_version_returns_conflict_without_overwriting_the_current_value()
    {
        await using var host = await TestHost.CreateAsync();
        var original = new DatabaseSettingUpdateDto<LlmAuditCleanupSettingsDto>(new()
        {
            Enabled = true,
            RetentionDays = 90
        }, null);
        var firstAction = await host.Controller.SaveLlmAuditCleanup(original, CancellationToken.None);
        var first = Assert.IsType<DatabaseSettingDto<LlmAuditCleanupSettingsDto>>(
            Assert.IsType<OkObjectResult>(firstAction).Value);

        var conflictAction = await host.Controller.SaveLlmAuditCleanup(
            original with { Value = new() { Enabled = true, RetentionDays = 1 } },
            CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(conflictAction);
        var details = Assert.IsType<DatabaseSettingConflictDto>(conflict.Value);
        Assert.Equal(LlmAuditCleanupOptions.SectionName, details.Key);
        var current = await host.LlmAuditCleanup.GetAsync();
        Assert.Equal(first.Version, current.Version);
        Assert.Equal(90, current.Value.RetentionDays);
    }

    [Fact]
    public async Task Invalid_configuration_returns_validation_problem_without_persisting()
    {
        await using var host = await TestHost.CreateAsync();
        var request = new DatabaseSettingUpdateDto<CertificationRequestCleanupSettingsDto>(new()
        {
            AutoRejectEnabled = true,
            PendingTimeoutDays = null
        }, null);

        var action = await host.Controller.SaveCertificationRequestCleanup(request, CancellationToken.None);

        var result = Assert.IsType<BadRequestObjectResult>(action);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.IsType<ValidationProblemDetails>(result.Value);
        Assert.Null((await host.CertificationRequestCleanup.GetAsync()).Version);
    }

    /// <summary>使用共享内存 SQLite 验证真实配置持久化，不访问站点数据库。</summary>
    private sealed class TestHost : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly ServiceProvider services;
        private readonly AsyncServiceScope scope;

        private TestHost(
            SqliteConnection connection,
            ServiceProvider services,
            AsyncServiceScope scope,
            string? actor)
        {
            this.connection = connection;
            this.services = services;
            this.scope = scope;
            Controller = ActivatorUtilities.CreateInstance<MaintenanceSettingsController>(scope.ServiceProvider);
            Controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        actor is null ? [] : [new Claim(ClaimTypes.NameIdentifier, actor)], "test"))
                }
            };
        }

        public MaintenanceSettingsController Controller { get; }

        public DatabaseSettings<LlmAuditCleanupOptions> LlmAuditCleanup =>
            scope.ServiceProvider.GetRequiredService<DatabaseSettings<LlmAuditCleanupOptions>>();

        public DatabaseSettings<CleanupScheduleOptions> CleanupSchedule =>
            scope.ServiceProvider.GetRequiredService<DatabaseSettings<CleanupScheduleOptions>>();

        public DatabaseSettings<CertificationRequestCleanupOptions> CertificationRequestCleanup =>
            scope.ServiceProvider.GetRequiredService<DatabaseSettings<CertificationRequestCleanupOptions>>();

        public static async Task<TestHost> CreateAsync(string? actor = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging();
            serviceCollection.AddSingleton<TimeProvider>(TimeProvider.System);
            serviceCollection.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
            serviceCollection.AddDatabaseSettings<CleanupScheduleOptions>(CleanupScheduleOptions.SectionName);
            serviceCollection.AddSingleton<IValidateOptions<AccountCleanupOptions>, AccountCleanupOptionsValidator>();
            serviceCollection.AddDatabaseSettings<AccountCleanupOptions>(AccountCleanupOptions.SectionName);
            serviceCollection.AddSingleton<IValidateOptions<CertificationRequestCleanupOptions>, CertificationRequestCleanupOptionsValidator>();
            serviceCollection.AddDatabaseSettings<CertificationRequestCleanupOptions>(CertificationRequestCleanupOptions.SectionName);
            serviceCollection.AddSingleton<IValidateOptions<LlmAuditCleanupOptions>, LlmAuditCleanupOptionsValidator>();
            serviceCollection.AddDatabaseSettings<LlmAuditCleanupOptions>(LlmAuditCleanupOptions.SectionName);
            var services = serviceCollection.BuildServiceProvider(
                new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
            try
            {
                await using (var scope = services.CreateAsyncScope())
                {
                    await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreatedAsync();
                }
                await services.LoadDatabaseSettingsAsync();
                return new TestHost(connection, services, services.CreateAsyncScope(), actor);
            }
            catch
            {
                await services.DisposeAsync();
                await connection.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await scope.DisposeAsync();
            await services.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
