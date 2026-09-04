using EzNutrition.Server.Controllers;
using EzNutrition.Server.Data;
using EzNutrition.Server.Extension;
using EzNutrition.Server.Services;
using EzNutrition.Server.Services.Maintenance;
using EzNutrition.Server.Services.Settings;
using EzNutrition.Shared.Data.DTO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace EzNutrition.Server.Tests.Controllers;

/// <summary>验证管理员账号清理接口固定配置版本并正确区分预览和执行。</summary>
public sealed class AccountCleanupControllerTests
{
    [Fact]
    public async Task Preview_uses_the_configured_retention_period_without_deleting_accounts()
    {
        await using var host = await TestHost.CreateAsync();
        var version = await host.SaveSettingsAsync(new()
        {
            CertificationSubmissionGraceDays = 7
        });
        await host.AddUserAsync("expired", TestHost.Now.AddDays(-8));
        await host.AddUserAsync("fresh", TestHost.Now.AddDays(-6));

        var action = await host.Controller.Preview(
            new(AccountCleanupRule.CertificationNotRequested), CancellationToken.None);

        var result = Assert.IsType<AccountCleanupOperationDto>(
            Assert.IsType<OkObjectResult>(action).Value);
        Assert.True(result.DryRun);
        Assert.Equal(version, result.SettingsVersion);
        Assert.Equal(TestHost.Now.AddDays(-7), result.CutoffUtc);
        var item = Assert.Single(result.Items);
        Assert.Equal("expired", item.UserId);
        Assert.Equal(AccountCleanupItemStatus.WouldDelete, item.Status);
        Assert.Equal(2, await host.CountUsersAsync());
    }

    [Fact]
    public async Task Execute_deletes_eligible_accounts_and_returns_the_completed_result()
    {
        await using var host = await TestHost.CreateAsync();
        await host.SaveSettingsAsync(new()
        {
            NonFormalAccountRetentionDays = 30
        });
        await host.AddUserAsync("roleless", TestHost.Now.AddDays(-31));
        await host.AddUserAsync("formal", TestHost.Now.AddDays(-31));
        await host.AddRoleAsync("formal", "Teacher");
        var preview = await PreviewAsync(host, AccountCleanupRule.AccountWithoutRoles);

        var action = await host.Controller.Execute(
            new(preview.Rule, preview.SettingsVersion, preview.CutoffUtc),
            CancellationToken.None);

        var result = Assert.IsType<AccountCleanupOperationDto>(
            Assert.IsType<OkObjectResult>(action).Value);
        Assert.False(result.DryRun);
        var item = Assert.Single(result.Items);
        Assert.Equal("roleless", item.UserId);
        Assert.Equal(AccountCleanupItemStatus.Deleted, item.Status);
        Assert.Equal(["formal"], await host.ReadUserIdsAsync());
    }

    [Fact]
    public async Task Execute_rejects_a_stale_settings_version_before_deleting_any_account()
    {
        await using var host = await TestHost.CreateAsync();
        var staleVersion = await host.SaveSettingsAsync(new()
        {
            NonFormalAccountRetentionDays = 30
        });
        await host.AddUserAsync("candidate", TestHost.Now.AddDays(-90));
        var preview = await PreviewAsync(host, AccountCleanupRule.AccountWithoutRoles);
        await host.SaveSettingsAsync(new()
        {
            NonFormalAccountRetentionDays = 60
        }, staleVersion);

        var action = await host.Controller.Execute(
            new(preview.Rule, preview.SettingsVersion, preview.CutoffUtc),
            CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(action);
        var details = Assert.IsType<DatabaseSettingConflictDto>(conflict.Value);
        Assert.Equal(AccountCleanupOptions.SectionName, details.Key);
        Assert.Equal(1, await host.CountUsersAsync());
    }

    [Fact]
    public async Task Preview_rejects_an_unpersisted_or_unconfigured_rule()
    {
        await using var host = await TestHost.CreateAsync();

        var unpersisted = await host.Controller.Preview(
            new(AccountCleanupRule.InactiveAccount), CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(unpersisted);

        await host.SaveSettingsAsync(new());
        var unconfigured = await host.Controller.Preview(
            new(AccountCleanupRule.InactiveAccount), CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(unconfigured);
        Assert.Equal(0, await host.CountUsersAsync());
    }

    [Fact]
    public async Task Execute_rejects_a_cutoff_that_would_expand_the_previewed_range()
    {
        await using var host = await TestHost.CreateAsync();
        await host.SaveSettingsAsync(new() { NonFormalAccountRetentionDays = 30 });
        await host.AddUserAsync("candidate", TestHost.Now.AddDays(-31));
        var preview = await PreviewAsync(host, AccountCleanupRule.AccountWithoutRoles);

        var action = await host.Controller.Execute(
            new(preview.Rule, preview.SettingsVersion, preview.CutoffUtc.AddSeconds(1)),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(action);
        Assert.Equal(1, await host.CountUsersAsync());
    }

    private static async Task<AccountCleanupOperationDto> PreviewAsync(
        TestHost host,
        AccountCleanupRule rule)
    {
        var action = await host.Controller.Preview(new(rule), CancellationToken.None);
        return Assert.IsType<AccountCleanupOperationDto>(
            Assert.IsType<OkObjectResult>(action).Value);
    }

    /// <summary>在独立 SQLite 数据库和临时证件目录中运行控制器及真实删除服务。</summary>
    private sealed class TestHost : IAsyncDisposable
    {
        internal static readonly DateTimeOffset Now =
            new(2026, 9, 4, 8, 0, 0, TimeSpan.Zero);

        private readonly SqliteConnection connection;
        private readonly string contentRoot;
        private readonly ServiceProvider services;
        private readonly AsyncServiceScope scope;

        private TestHost(
            SqliteConnection connection,
            string contentRoot,
            ServiceProvider services,
            AsyncServiceScope scope)
        {
            this.connection = connection;
            this.contentRoot = contentRoot;
            this.services = services;
            this.scope = scope;
            Controller = ActivatorUtilities.CreateInstance<AccountCleanupController>(scope.ServiceProvider);
        }

        public AccountCleanupController Controller { get; }

        public static async Task<TestHost> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            var contentRoot = Path.Combine(
                Path.GetTempPath(), "EzNutrition.AccountCleanupController.Tests", Guid.NewGuid().ToString("N"));
            await connection.OpenAsync();
            Directory.CreateDirectory(contentRoot);

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging();
            serviceCollection.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
            serviceCollection.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>();
            serviceCollection.AddSingleton<TimeProvider>(new FixedTimeProvider());
            serviceCollection.AddSingleton<IWebHostEnvironment>(new TestEnvironment(contentRoot));
            serviceCollection.AddSingleton<CertificateFileStore>();
            serviceCollection.AddScoped<AccountDeletionService>();
            serviceCollection.AddScoped<AccountCleanupService>();
            serviceCollection.AddSingleton<IValidateOptions<AccountCleanupOptions>, AccountCleanupOptionsValidator>();
            serviceCollection.AddDatabaseSettings<AccountCleanupOptions>(AccountCleanupOptions.SectionName);
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
                return new TestHost(connection, contentRoot, services, services.CreateAsyncScope());
            }
            catch
            {
                await services.DisposeAsync();
                await connection.DisposeAsync();
                Directory.Delete(contentRoot, recursive: true);
                throw;
            }
        }

        public async Task<Guid> SaveSettingsAsync(
            AccountCleanupOptions value,
            Guid? expectedVersion = null)
        {
            var saved = await scope.ServiceProvider.GetRequiredService<DatabaseSettings<AccountCleanupOptions>>()
                .SaveAsync(value, expectedVersion);
            return Assert.IsType<Guid>(saved.Version);
        }

        public async Task AddUserAsync(string id, DateTimeOffset createdAt)
        {
            await using var context = CreateContext();
            context.Users.Add(new ApplicationUser
            {
                Id = id,
                UserName = id,
                NormalizedUserName = id.ToUpperInvariant(),
                CreatedAtUtc = createdAt.UtcDateTime,
                ConcurrencyStamp = Guid.NewGuid().ToString()
            });
            await context.SaveChangesAsync();
        }

        public async Task AddRoleAsync(string userId, string roleName)
        {
            await using var context = CreateContext();
            context.Roles.Add(new IdentityRole(roleName) { Id = roleName });
            context.UserRoles.Add(new IdentityUserRole<string> { UserId = userId, RoleId = roleName });
            await context.SaveChangesAsync();
        }

        public async Task<int> CountUsersAsync()
        {
            await using var context = CreateContext();
            return await context.Users.CountAsync();
        }

        public async Task<string[]> ReadUserIdsAsync()
        {
            await using var context = CreateContext();
            return await context.Users.OrderBy(user => user.Id).Select(user => user.Id).ToArrayAsync();
        }

        private ApplicationDbContext CreateContext() => new(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);

        public async ValueTask DisposeAsync()
        {
            await scope.DisposeAsync();
            await services.DisposeAsync();
            await connection.DisposeAsync();
            Directory.Delete(contentRoot, recursive: true);
        }

        private sealed class FixedTimeProvider : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => Now;
        }

        private sealed class TestEnvironment(string contentRoot) : IWebHostEnvironment
        {
            public string ApplicationName { get; set; } = "AccountCleanupControllerTests";
            public string EnvironmentName { get; set; } = "Development";
            public string ContentRootPath { get; set; } = contentRoot;
            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
            public string WebRootPath { get; set; } = contentRoot;
            public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        }
    }
}
