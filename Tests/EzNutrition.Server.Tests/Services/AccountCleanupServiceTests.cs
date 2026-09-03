using EzNutrition.Server.Data;
using EzNutrition.Server.Data.Entities;
using EzNutrition.Server.Services;
using EzNutrition.Server.Services.Maintenance;
using EzNutrition.Shared.Data.DTO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using System.Data;
using System.Data.Common;

namespace EzNutrition.Server.Tests.Services;

public sealed class AccountCleanupServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Cutoff = Now.AddDays(-30);

    [Fact]
    public async Task Inactive_preview_uses_login_or_creation_without_filtering_roles_or_applications()
    {
        await using var host = new TestHost();
        await host.AddUserAsync("old-login", Cutoff.AddDays(-10), Cutoff.AddDays(-1));
        await host.AddUserAsync("never-logged-in", Cutoff.AddDays(-10));
        await host.AddUserAsync("admin", Cutoff.AddDays(-10), Cutoff.AddDays(-1));
        await host.AddRoleAsync("admin", "Admin");
        await host.AddRequestAsync("old-login", RequestStatus.Approved);
        await host.AddUserAsync("recent-login", Cutoff.AddDays(-10), Cutoff.AddDays(1));
        await host.AddUserAsync("recent-registration", Cutoff.AddDays(1));
        await host.AddUserAsync("login-boundary", Cutoff.AddDays(-10), Cutoff);
        await host.AddUserAsync("creation-boundary", Cutoff);
        await host.AddUserAsync("unknown", DateTimeOffset.MinValue);
        await host.AddUserAsync("unknown-creation-known-login", DateTimeOffset.MinValue, Cutoff.AddDays(-1));
        var request = await host.GetRequestAsync("old-login");
        var certificatePath = await host.SaveCertificateAsync(request.CertificateTicket!.Value);

        var result = await host.Service.DeleteInactiveAccountsAsync(Cutoff.ToOffset(TimeSpan.FromHours(8)));

        Assert.True(result.DryRun);
        Assert.False(result.IsCanceled);
        Assert.Equal(TimeSpan.Zero, result.CutoffUtc.Offset);
        Assert.Equal(Cutoff, result.CutoffUtc);
        Assert.Equal(["admin", "never-logged-in", "old-login", "unknown-creation-known-login"],
            result.Items.Select(item => item.UserId));
        Assert.All(result.Items, item => Assert.Equal(AccountCleanupStatus.WouldDelete, item.Status));
        Assert.Equal(9, await host.CountUsersAsync());
        Assert.True(File.Exists(certificatePath));
        Assert.NotNull(await host.GetRequestAsync("old-login"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Roleless_preview_excludes_roles_and_optionally_every_application_status(bool onlyWithoutApplications)
    {
        await using var host = new TestHost();
        await host.AddUserAsync("none", Cutoff.AddDays(-1), Now);
        foreach (var status in Enum.GetValues<RequestStatus>())
        {
            await host.AddUserAsync(status.ToString(), Cutoff.AddDays(-1));
            await host.AddRequestAsync(status.ToString(), status);
        }
        await host.AddUserAsync("custom-role", Cutoff.AddDays(-1));
        await host.AddRoleAsync("custom-role", "CustomValidRole");
        await host.AddUserAsync("boundary", Cutoff);
        await host.AddUserAsync("recent", Now);
        await host.AddUserAsync("unknown", DateTimeOffset.MinValue);

        var result = await host.Service.DeleteAccountsWithoutRolesAsync(Cutoff, onlyWithoutApplications);

        var expected = onlyWithoutApplications ? ["none"] : new[] { "Approved", "Pending", "Rejected", "none" };
        Assert.Equal(expected.Order(), result.Items.Select(item => item.UserId).Order());
        Assert.All(result.Items, item => Assert.Equal(AccountCleanupStatus.WouldDelete, item.Status));
        Assert.Equal(8, await host.CountUsersAsync());
    }

    [Fact]
    public async Task Execution_reuses_account_deletion_and_preserves_other_users()
    {
        var transactions = new TransactionHooks();
        await using var host = new TestHost(transactions);
        await host.AddUserAsync("target", Cutoff.AddDays(-1));
        await host.AddUserAsync("retained", Now);
        await host.AddRoleAsync("target", "Admin");
        await host.AddRequestAsync("target", RequestStatus.Pending);
        await host.AddAuditAsync("target");
        var path = await host.SaveCertificateAsync((await host.GetRequestAsync("target")).CertificateTicket!.Value);
        transactions.StartedIsolationLevels.Clear();

        var result = await host.Service.DeleteInactiveAccountsAsync(Cutoff, dryRun: false);

        Assert.False(result.DryRun);
        var item = Assert.Single(result.Items);
        Assert.Equal("target", item.UserId);
        Assert.Equal(AccountCleanupStatus.Deleted, item.Status);
        Assert.Equal(0, item.CertificateFileCleanupFailures);
        Assert.Equal(IsolationLevel.Serializable, Assert.Single(transactions.StartedIsolationLevels));
        await using var context = host.CreateContext();
        Assert.Equal(["retained"], await context.Users.Select(user => user.Id).ToArrayAsync());
        Assert.Empty(await context.ProfessionalCertificationRequests.ToArrayAsync());
        Assert.Empty(await context.PrescriptionGenerateRequests.ToArrayAsync());
        Assert.Empty(await context.UserRoles.ToArrayAsync());
        Assert.False(File.Exists(path));
    }

    [Theory]
    [InlineData("login")]
    [InlineData("role")]
    [InlineData("Pending")]
    [InlineData("Approved")]
    [InlineData("Rejected")]
    [InlineData("deleted")]
    public async Task Changes_after_selection_are_rechecked_inside_the_deletion_transaction(string change)
    {
        var transactions = new TransactionHooks();
        await using var host = new TestHost(transactions);
        await host.AddUserAsync("target", Cutoff.AddDays(-1));
        transactions.BeforeNextTransaction = async () =>
        {
            if (change == "login")
            {
                await using var context = host.CreateContext();
                var user = await context.Users.SingleAsync();
                user.LastSuccessfulLoginAtUtc = Now.UtcDateTime;
                await context.SaveChangesAsync();
            }
            else if (change == "role")
            {
                await host.AddRoleAsync("target", "Teacher");
            }
            else if (change == "deleted")
            {
                await using var context = host.CreateContext();
                await context.Users.ExecuteDeleteAsync();
            }
            else
            {
                await host.AddRequestAsync("target", Enum.Parse<RequestStatus>(change));
            }
        };

        var result = change == "login"
            ? await host.Service.DeleteInactiveAccountsAsync(Cutoff, dryRun: false)
            : await host.Service.DeleteAccountsWithoutRolesAsync(Cutoff, true, dryRun: false);

        Assert.Equal(AccountCleanupStatus.Skipped, Assert.Single(result.Items).Status);
        Assert.Equal(change == "deleted" ? 0 : 1, await host.CountUsersAsync());
    }

    [Fact]
    public async Task Application_added_after_selection_does_not_block_deletion_when_the_check_is_disabled()
    {
        var transactions = new TransactionHooks();
        await using var host = new TestHost(transactions);
        await host.AddUserAsync("target", Cutoff.AddDays(-1));
        transactions.BeforeNextTransaction = () => host.AddRequestAsync("target", RequestStatus.Approved);

        var result = await host.Service.DeleteAccountsWithoutRolesAsync(Cutoff, false, dryRun: false);

        Assert.Equal(AccountCleanupStatus.Deleted, Assert.Single(result.Items).Status);
        Assert.Equal(0, await host.CountUsersAsync());
        await using var context = host.CreateContext();
        Assert.Empty(await context.ProfessionalCertificationRequests.ToArrayAsync());
    }

    [Fact]
    public async Task Identity_failure_rolls_back_related_deletions_and_allows_other_accounts_to_continue()
    {
        var failures = new DeletionFailureInterceptor("01-failed");
        await using var host = new TestHost(failures);
        await host.AddUserAsync("01-failed", Cutoff.AddDays(-1));
        await host.AddUserAsync("02-deleted", Cutoff.AddDays(-1));
        await host.AddAuditAsync("01-failed");
        await host.AddRequestAsync("01-failed", RequestStatus.Pending);
        var path = await host.SaveCertificateAsync((await host.GetRequestAsync("01-failed")).CertificateTicket!.Value);

        var result = await host.Service.DeleteInactiveAccountsAsync(Cutoff, dryRun: false);

        Assert.Equal([AccountCleanupStatus.Failed, AccountCleanupStatus.Deleted], result.Items.Select(item => item.Status));
        Assert.Contains("ConcurrencyFailure", result.Items[0].Reason);
        Assert.Equal(1, await host.CountUsersAsync());
        await using var context = host.CreateContext();
        Assert.Single(await context.PrescriptionGenerateRequests.ToArrayAsync());
        Assert.Single(await context.ProfessionalCertificationRequests.ToArrayAsync());
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task Database_exception_is_reported_per_account_without_aborting_the_remaining_work()
    {
        var transactions = new TransactionHooks();
        await using var host = new TestHost(transactions);
        await host.AddUserAsync("01-failed", Cutoff.AddDays(-1));
        await host.AddUserAsync("02-deleted", Cutoff.AddDays(-1));
        transactions.BeforeNextTransaction = () => throw new InvalidOperationException("simulated database failure");

        var result = await host.Service.DeleteInactiveAccountsAsync(Cutoff, dryRun: false);

        Assert.Equal([AccountCleanupStatus.Failed, AccountCleanupStatus.Deleted], result.Items.Select(item => item.Status));
        Assert.Equal(1, await host.CountUsersAsync());
    }

    [WindowsFact]
    public async Task File_failure_is_a_warning_on_a_successfully_deleted_account()
    {
        await using var host = new TestHost();
        await host.AddUserAsync("target", Cutoff.AddDays(-1));
        await host.AddRequestAsync("target", RequestStatus.Pending);
        var path = await host.SaveCertificateAsync((await host.GetRequestAsync("target")).CertificateTicket!.Value);
        using var lockedFile = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        var result = await host.Service.DeleteInactiveAccountsAsync(Cutoff, dryRun: false);

        var item = Assert.Single(result.Items);
        Assert.Equal(AccountCleanupStatus.Deleted, item.Status);
        Assert.Equal(1, item.CertificateFileCleanupFailures);
        Assert.Equal(0, await host.CountUsersAsync());
    }

    [Fact]
    public async Task Cancellation_after_a_commit_returns_completed_and_unprocessed_items()
    {
        using var cancellation = new CancellationTokenSource();
        var transactions = new TransactionHooks();
        await using var host = new TestHost(transactions);
        await host.AddUserAsync("01-deleted", Cutoff.AddDays(-1));
        await host.AddUserAsync("02-pending", Cutoff.AddDays(-1));
        transactions.AfterNextCommit = cancellation.Cancel;

        var result = await host.Service.DeleteInactiveAccountsAsync(Cutoff, dryRun: false, cancellation.Token);

        Assert.True(result.IsCanceled);
        Assert.Equal([AccountCleanupStatus.Deleted, AccountCleanupStatus.WouldDelete], result.Items.Select(item => item.Status));
        Assert.Equal(1, await host.CountUsersAsync());
    }

    [Fact]
    public async Task Cancellation_before_selection_performs_no_deletion()
    {
        await using var host = new TestHost();
        await host.AddUserAsync("target", Cutoff.AddDays(-1));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            host.Service.DeleteInactiveAccountsAsync(Cutoff, false, cancellation.Token));
        Assert.Equal(1, await host.CountUsersAsync());
    }

    [Fact]
    public async Task Preview_returns_all_candidates_without_an_implicit_limit()
    {
        await using var host = new TestHost();
        await using (var context = host.CreateContext())
        {
            context.Users.AddRange(Enumerable.Range(0, 1001).Select(index => new ApplicationUser
            {
                Id = index.ToString(), UserName = $"user-{index}", CreatedAtUtc = Cutoff.AddDays(-1).UtcDateTime
            }));
            await context.SaveChangesAsync();
        }

        var result = await host.Service.DeleteAccountsWithoutRolesAsync(Cutoff, false);

        Assert.Equal(1001, result.Items.Count);
        Assert.Equal(1001, await host.CountUsersAsync());
    }

    [Fact]
    public async Task Cleanup_does_not_save_changes_tracked_by_the_calling_scope()
    {
        await using var host = new TestHost();
        await host.AddUserAsync("target", Cutoff.AddDays(-1));
        await using var scope = host.Provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Users.Add(new ApplicationUser { Id = "unsaved", UserName = "unsaved" });

        var result = await scope.ServiceProvider.GetRequiredService<AccountCleanupService>()
            .DeleteInactiveAccountsAsync(Cutoff, false);

        Assert.Equal(AccountCleanupStatus.Deleted, Assert.Single(result.Items).Status);
        Assert.Equal(0, await host.CountUsersAsync());
        Assert.Single(context.ChangeTracker.Entries<ApplicationUser>());
    }

    [Fact]
    public async Task Invalid_cutoff_and_ambient_transactions_are_rejected_before_work()
    {
        await using var host = new TestHost();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => host.Service.DeleteInactiveAccountsAsync(Now.AddDays(1)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => host.Service.DeleteAccountsWithoutRolesAsync(DateTimeOffset.MinValue, true));
        using var transaction = new System.Transactions.TransactionScope(System.Transactions.TransactionScopeAsyncFlowOption.Enabled);
        await Assert.ThrowsAsync<InvalidOperationException>(() => host.Service.DeleteInactiveAccountsAsync(Cutoff, false));
    }

    /// <summary>使用独立内存数据库和临时证件目录，避免访问部署环境的数据。</summary>
    private sealed class TestHost : IAsyncDisposable
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");
        private readonly string contentRoot = Path.Combine(Path.GetTempPath(), "EzNutrition.AccountCleanup.Tests", Guid.NewGuid().ToString("N"));
        public ServiceProvider Provider { get; }
        public AccountCleanupService Service { get; }

        public TestHost(params IInterceptor[] interceptors)
        {
            connection.Open();
            Directory.CreateDirectory(contentRoot);
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection).AddInterceptors(interceptors));
            services.AddIdentity<ApplicationUser, IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
            services.AddSingleton<TimeProvider>(new FixedTimeProvider());
            services.AddSingleton<IWebHostEnvironment>(new TestEnvironment(contentRoot));
            services.AddSingleton<CertificateFileStore>();
            services.AddScoped<AccountDeletionService>();
            services.AddScoped<AccountCleanupService>();
            Provider = services.BuildServiceProvider(validateScopes: true);
            Service = ActivatorUtilities.CreateInstance<AccountCleanupService>(Provider);
            using var context = CreateContext();
            context.Database.EnsureCreated();
        }

        public ApplicationDbContext CreateContext() => new(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);

        public async Task AddUserAsync(string id, DateTimeOffset createdAt, DateTimeOffset? lastLogin = null)
        {
            await using var context = CreateContext();
            context.Users.Add(new ApplicationUser
            {
                Id = id, UserName = id, NormalizedUserName = id.ToUpperInvariant(),
                CreatedAtUtc = createdAt.UtcDateTime, LastSuccessfulLoginAtUtc = lastLogin?.UtcDateTime,
                ConcurrencyStamp = Guid.NewGuid().ToString()
            });
            await context.SaveChangesAsync();
        }

        public async Task AddRoleAsync(string userId, string role)
        {
            await using var context = CreateContext();
            context.Roles.Add(new IdentityRole(role) { Id = role });
            context.UserRoles.Add(new IdentityUserRole<string> { RoleId = role, UserId = userId });
            await context.SaveChangesAsync();
        }

        public async Task AddRequestAsync(string userId, RequestStatus status)
        {
            await using var context = CreateContext();
            context.ProfessionalCertificationRequests.Add(new()
            {
                Id = Guid.NewGuid(), UserId = userId, Status = status, RequestTime = Cutoff.AddDays(-1).UtcDateTime,
                IdentityType = "Teacher", InstitutionName = "Test", CertificateTicket = Guid.NewGuid()
            });
            await context.SaveChangesAsync();
        }

        public async Task<ProfessionalCertificationRequest> GetRequestAsync(string userId)
        {
            await using var context = CreateContext();
            return await context.ProfessionalCertificationRequests.SingleAsync(request => request.UserId == userId);
        }

        public async Task AddAuditAsync(string userId)
        {
            await using var context = CreateContext();
            context.PrescriptionGenerateRequests.Add(new() { Id = Guid.NewGuid(), UserId = userId, Prompt = "audit" });
            await context.SaveChangesAsync();
        }

        public async Task<string> SaveCertificateAsync(Guid ticket)
        {
            byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];
            await using var stream = new MemoryStream(png);
            await Provider.GetRequiredService<CertificateFileStore>()
                .SaveAsync(ticket, new FormFile(stream, 0, png.Length, "file", "test.png"), CancellationToken.None);
            return Path.Combine(contentRoot, "TempUploads", $"{ticket:D}.png");
        }

        public async Task<int> CountUsersAsync()
        {
            await using var context = CreateContext();
            return await context.Users.CountAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await Provider.DisposeAsync();
            await connection.DisposeAsync();
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    /// <summary>模拟初筛后发生的变化，并观察实际删除事务使用的隔离级别。</summary>
    private sealed class TransactionHooks : DbTransactionInterceptor
    {
        public Func<Task>? BeforeNextTransaction { get; set; }
        public Action? AfterNextCommit { get; set; }
        public List<IsolationLevel> StartedIsolationLevels { get; } = [];

        public override async ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(
            DbConnection connection, TransactionStartingEventData eventData, InterceptionResult<DbTransaction> result,
            CancellationToken cancellationToken = default)
        {
            StartedIsolationLevels.Add(eventData.IsolationLevel);
            var callback = BeforeNextTransaction;
            BeforeNextTransaction = null;
            if (callback is not null)
            {
                await callback();
            }
            return result;
        }

        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            var callback = AfterNextCommit;
            AfterNextCommit = null;
            callback?.Invoke();
            return Task.CompletedTask;
        }
    }

    /// <summary>在 Identity 删除阶段触发并发失败，验证此前的关联数据删除会回滚。</summary>
    private sealed class DeletionFailureInterceptor(string userId) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<ApplicationUser>()
                .Any(entry => entry.State == EntityState.Deleted && entry.Entity.Id == userId))
            {
                throw new DbUpdateConcurrencyException("simulated Identity concurrency failure");
            }
            return ValueTask.FromResult(result);
        }
    }

    /// <summary>此测试依赖 Windows 的文件共享删除限制，其他平台明确跳过。</summary>
    private sealed class WindowsFactAttribute : FactAttribute
    {
        public WindowsFactAttribute()
        {
            if (!OperatingSystem.IsWindows())
            {
                Skip = "需要 Windows 的 FileShare 删除限制。";
            }
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class TestEnvironment(string contentRoot) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "AccountCleanupTests";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = contentRoot;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
