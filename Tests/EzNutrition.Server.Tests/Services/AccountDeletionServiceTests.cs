using EzNutrition.Server.Data;
using EzNutrition.Server.Data.Entities;
using EzNutrition.Server.Services;
using EzNutrition.Shared.Data.DTO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using System.Security.Claims;

namespace EzNutrition.Server.Tests.Services;

public sealed class AccountDeletionServiceTests
{
    [Fact]
    public async Task DeleteAsync_removes_the_account_and_only_its_related_data()
    {
        await using var host = TestHost.Create();
        var targetUser = await host.CreateUserAsync("target-user");
        var retainedUser = await host.CreateUserAsync("retained-user");
        var targetTicket = Guid.NewGuid();
        var retainedTicket = Guid.NewGuid();

        await host.AddIdentityDataAsync(targetUser);
        host.DbContext.PrescriptionGenerateRequests.AddRange(
            CreateAiAudit(targetUser.Id),
            CreateAiAudit(targetUser.Id),
            CreateAiAudit(retainedUser.Id));
        host.DbContext.ProfessionalCertificationRequests.AddRange(
            CreateCertificationRequest(targetUser.Id, targetTicket),
            CreateCertificationRequest(targetUser.Id, targetTicket),
            CreateCertificationRequest(retainedUser.Id, retainedTicket));
        await host.DbContext.SaveChangesAsync();
        await host.SaveCertificateAsync(targetTicket);
        await host.SaveCertificateAsync(retainedTicket);

        var result = await host.Service.DeleteAsync(
            targetUser.Id,
            AccountDeletionReason.UserRequested);

        Assert.True(result.AccountFound);
        Assert.True(result.Succeeded);
        Assert.Equal(2, result.DeletedAiAuditRecords);
        Assert.Equal(2, result.DeletedCertificationRequests);
        Assert.Equal(1, result.CertificateFileCleanupAttempts);
        Assert.Equal(0, result.CertificateFileCleanupFailures);
        Assert.Empty(result.IdentityErrors);

        Assert.False(await host.DbContext.Users.AnyAsync(user => user.Id == targetUser.Id));
        Assert.True(await host.DbContext.Users.AnyAsync(user => user.Id == retainedUser.Id));
        Assert.False(await host.DbContext.UserClaims.AnyAsync(claim => claim.UserId == targetUser.Id));
        Assert.False(await host.DbContext.UserLogins.AnyAsync(login => login.UserId == targetUser.Id));
        Assert.False(await host.DbContext.UserRoles.AnyAsync(role => role.UserId == targetUser.Id));
        Assert.False(await host.DbContext.UserTokens.AnyAsync(token => token.UserId == targetUser.Id));
        Assert.Equal(
            [retainedUser.Id],
            await host.DbContext.PrescriptionGenerateRequests
                .Select(request => request.UserId)
                .ToArrayAsync());
        Assert.Equal(
            [retainedUser.Id],
            await host.DbContext.ProfessionalCertificationRequests
                .Select(request => request.UserId)
                .ToArrayAsync());

        Assert.Null(host.CertificateFileStore.OpenRead(targetTicket));
        var retainedCertificate = host.CertificateFileStore.OpenRead(retainedTicket);
        Assert.NotNull(retainedCertificate);
        retainedCertificate.Content.Dispose();
    }

    [Fact]
    public async Task DeleteAsync_is_idempotent_for_orphaned_user_data()
    {
        await using var host = TestHost.Create();
        const string missingUserId = "already-deleted-user";
        var certificateTicket = Guid.NewGuid();
        host.DbContext.PrescriptionGenerateRequests.Add(CreateAiAudit(missingUserId));
        host.DbContext.ProfessionalCertificationRequests.Add(
            CreateCertificationRequest(missingUserId, certificateTicket));
        await host.DbContext.SaveChangesAsync();
        await host.SaveCertificateAsync(certificateTicket);

        var result = await host.Service.DeleteAsync(
            missingUserId,
            AccountDeletionReason.InactiveAccountExpired);

        Assert.False(result.AccountFound);
        Assert.True(result.Succeeded);
        Assert.Equal(1, result.DeletedAiAuditRecords);
        Assert.Equal(1, result.DeletedCertificationRequests);
        Assert.Equal(1, result.CertificateFileCleanupAttempts);
        Assert.Equal(0, result.CertificateFileCleanupFailures);
        Assert.Empty(host.DbContext.PrescriptionGenerateRequests);
        Assert.Empty(host.DbContext.ProfessionalCertificationRequests);
        Assert.Null(host.CertificateFileStore.OpenRead(certificateTicket));
    }

    [Fact]
    public async Task DeleteAsync_discards_unsaved_tracked_user_data_before_deleting_identity()
    {
        await using var host = TestHost.Create();
        var user = await host.CreateUserAsync("registration-rollback-user");
        var certificateTicket = Guid.NewGuid();
        var unsavedAiAudit = CreateAiAudit(user.Id);
        var unsavedCertification = CreateCertificationRequest(user.Id, certificateTicket);
        host.DbContext.PrescriptionGenerateRequests.Add(unsavedAiAudit);
        host.DbContext.ProfessionalCertificationRequests.Add(unsavedCertification);
        await host.SaveCertificateAsync(certificateTicket);

        var result = await host.Service.DeleteAsync(
            user.Id,
            AccountDeletionReason.RegistrationRollback);

        Assert.True(result.AccountFound);
        Assert.True(result.Succeeded);
        Assert.Equal(0, result.DeletedAiAuditRecords);
        Assert.Equal(0, result.DeletedCertificationRequests);
        Assert.Equal(1, result.CertificateFileCleanupAttempts);
        Assert.False(await host.DbContext.Users.AnyAsync(candidate => candidate.Id == user.Id));
        Assert.Empty(host.DbContext.PrescriptionGenerateRequests);
        Assert.Empty(host.DbContext.ProfessionalCertificationRequests);
        Assert.Null(host.CertificateFileStore.OpenRead(certificateTicket));
    }

    private static PrescriptionGenerateRequest CreateAiAudit(string userId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Prompt = "audit prompt",
        Content = "audit response",
        RequestTime = DateTime.UtcNow,
        ProcessedTime = DateTime.UtcNow
    };

    private static ProfessionalCertificationRequest CreateCertificationRequest(
        string userId,
        Guid certificateTicket) => new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RequestTime = DateTime.UtcNow,
            IdentityType = "PublicHealthPhysician",
            InstitutionName = "Test Institution",
            Status = RequestStatus.Pending,
            CertificateTicket = certificateTicket
        };

    private sealed class TestHost : IAsyncDisposable
    {
        private const string ProfessionalRole = "Professional";
        private readonly ServiceProvider rootProvider;
        private readonly AsyncServiceScope scope;
        private readonly SqliteConnection connection;
        private readonly string contentRootPath;

        private TestHost(
            ServiceProvider rootProvider,
            AsyncServiceScope scope,
            SqliteConnection connection,
            string contentRootPath)
        {
            this.rootProvider = rootProvider;
            this.scope = scope;
            this.connection = connection;
            this.contentRootPath = contentRootPath;
            DbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            UserManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            RoleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            CertificateFileStore = scope.ServiceProvider.GetRequiredService<CertificateFileStore>();
            Service = scope.ServiceProvider.GetRequiredService<AccountDeletionService>();
        }

        internal ApplicationDbContext DbContext { get; }

        internal UserManager<ApplicationUser> UserManager { get; }

        internal RoleManager<IdentityRole> RoleManager { get; }

        internal CertificateFileStore CertificateFileStore { get; }

        internal AccountDeletionService Service { get; }

        internal static TestHost Create()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            var contentRootPath = Path.Combine(
                Path.GetTempPath(),
                "EzNutrition.Server.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(contentRootPath);

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
            services
                .AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>();
            services.AddSingleton<IWebHostEnvironment>(
                new TestWebHostEnvironment(contentRootPath));
            services.AddSingleton<CertificateFileStore>();
            services.AddScoped<AccountDeletionService>();

            var rootProvider = services.BuildServiceProvider(validateScopes: true);
            var scope = rootProvider.CreateAsyncScope();
            scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>()
                .Database
                .EnsureCreated();
            return new TestHost(rootProvider, scope, connection, contentRootPath);
        }

        internal async Task<ApplicationUser> CreateUserAsync(string userName)
        {
            var user = new ApplicationUser { UserName = userName };
            var result = await UserManager.CreateAsync(user);
            Assert.True(
                result.Succeeded,
                string.Join(", ", result.Errors.Select(error => error.Description)));
            return user;
        }

        internal async Task AddIdentityDataAsync(ApplicationUser user)
        {
            var createRoleResult = await RoleManager.CreateAsync(new IdentityRole(ProfessionalRole));
            Assert.True(createRoleResult.Succeeded);
            Assert.True((await UserManager.AddToRoleAsync(user, ProfessionalRole)).Succeeded);
            Assert.True((await UserManager.AddClaimAsync(user, new Claim("profession", "verified"))).Succeeded);
            Assert.True((await UserManager.AddLoginAsync(
                user,
                new UserLoginInfo("test-provider", "target-key", "Test Provider"))).Succeeded);
            Assert.True((await UserManager.SetAuthenticationTokenAsync(
                user,
                "test-provider",
                "access-token",
                "sensitive-value")).Succeeded);
        }

        internal async Task SaveCertificateAsync(Guid ticket)
        {
            byte[] pngBytes =
            [
                0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
                0x00, 0x00, 0x00, 0x00
            ];
            await using var content = new MemoryStream(pngBytes);
            var file = new FormFile(content, 0, content.Length, "certificate", "certificate.png");
            await CertificateFileStore.SaveAsync(ticket, file, CancellationToken.None);
        }

        public async ValueTask DisposeAsync()
        {
            await scope.DisposeAsync();
            await rootProvider.DisposeAsync();
            await connection.DisposeAsync();
            if (Directory.Exists(contentRootPath))
            {
                Directory.Delete(contentRootPath, recursive: true);
            }
        }
    }

    private sealed class TestWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = typeof(AccountDeletionServiceTests).Assembly.FullName!;

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string WebRootPath { get; set; } = contentRootPath;

        public string EnvironmentName { get; set; } = Environments.Development;

        public string ContentRootPath { get; set; } = contentRootPath;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
