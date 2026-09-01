using EzNutrition.Server.Data;
using EzNutrition.Server.Data.Repositories;
using EzNutrition.Server.Services;
using EzNutrition.Server.Services.Settings;
using EzNutrition.Shared.Data.DTO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;

namespace EzNutrition.Server.Tests.Repositories;

public sealed class AuthManagerRepositoryLoginTests
{
    [Fact]
    public async Task User_with_email_is_rejected_until_the_email_is_confirmed()
    {
        await using var host = LoginTestHost.Create();
        var user = await host.CreateUserAsync(
            "pending-user",
            "pending@example.test",
            emailConfirmed: false);

        var correctPasswordFailure = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            host.Repository.Login(user.UserName!, LoginTestHost.InitialPassword));
        var wrongPasswordFailure = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            host.Repository.Login(user.UserName!, "wrong-password"));

        Assert.Equal(wrongPasswordFailure.Message, correctPasswordFailure.Message);

        var confirmationToken = await host.UserManager.GenerateEmailConfirmationTokenAsync(user);
        var confirmation = await host.UserManager.ConfirmEmailAsync(user, confirmationToken);
        Assert.True(
            confirmation.Succeeded,
            string.Join(", ", confirmation.Errors.Select(error => error.Description)));

        var accessToken = await host.Repository.Login(
            user.UserName!,
            LoginTestHost.InitialPassword);

        AssertJwt(accessToken);
    }

    [Fact]
    public async Task Email_less_bootstrap_admin_can_still_log_in()
    {
        await using var host = LoginTestHost.Create();

        await host.Repository.Initialize();

        var admin = await host.UserManager.FindByNameAsync("Admin");
        Assert.NotNull(admin);
        Assert.Null(admin.Email);
        Assert.False(admin.EmailConfirmed);

        var accessToken = await host.Repository.Login(
            "Admin",
            LoginTestHost.InitialPassword);

        AssertJwt(accessToken);
    }

    [Fact]
    public async Task Registration_initialization_failure_uses_common_account_deletion()
    {
        await using var host = LoginTestHost.Create(failEmailConfirmation: true);
        var registration = new RegistrationDto
        {
            UserName = "rollback-user",
            Password = LoginTestHost.InitialPassword,
            Email = "rollback@example.test",
            ProfessionalIdentity = new ProfessionalIdentityDto
            {
                IdentityType = "Physician",
                InstitutionName = "Test Institution"
            }
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            host.Repository.RegisterUserAsync(registration));

        Assert.Equal(TestAccountEmailSender.FailureMessage, exception.Message);
        Assert.Null(await host.UserManager.FindByNameAsync(registration.UserName));
        Assert.Empty(await host.DbContext.ProfessionalCertificationRequests.ToArrayAsync());
        Assert.Empty(await host.DbContext.PrescriptionGenerateRequests.ToArrayAsync());
    }

    private static void AssertJwt(string accessToken)
    {
        Assert.False(string.IsNullOrWhiteSpace(accessToken));
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        Assert.Equal("EzPreventive", jwt.Issuer);
        Assert.Contains("EzNutrition", jwt.Audiences);
        Assert.Contains(jwt.Claims, claim => claim.Type == JwtService.SecurityStampClaimType);
    }

    private sealed class LoginTestHost : IAsyncDisposable
    {
        internal const string InitialPassword = "initial-password";

        private readonly ServiceProvider rootProvider;
        private readonly AsyncServiceScope scope;
        private readonly SqliteConnection connection;
        private readonly string contentRootPath;

        private LoginTestHost(
            ServiceProvider rootProvider,
            AsyncServiceScope scope,
            SqliteConnection connection,
            string contentRootPath)
        {
            this.rootProvider = rootProvider;
            this.scope = scope;
            this.connection = connection;
            this.contentRootPath = contentRootPath;
            UserManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            Repository = scope.ServiceProvider.GetRequiredService<AuthManagerRepository>();
            DbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        }

        internal UserManager<IdentityUser> UserManager { get; }

        internal AuthManagerRepository Repository { get; }

        internal ApplicationDbContext DbContext { get; }

        internal static LoginTestHost Create(bool failEmailConfirmation = false)
        {
            using var rsa = RSA.Create(2048);
            var privateKey = Convert.ToBase64String(rsa.ExportPkcs8PrivateKey());
            var publicKey = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            var contentRootPath = Path.Combine(
                Path.GetTempPath(),
                "EzNutrition.Server.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(contentRootPath);

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(connection));
            services
                .AddIdentity<IdentityUser, IdentityRole>(options =>
                {
                    options.Password.RequireDigit = false;
                    options.Password.RequiredLength = 6;
                    options.Password.RequireNonAlphanumeric = false;
                    options.Password.RequireUppercase = false;
                    options.Password.RequireLowercase = false;
                    options.Lockout.AllowedForNewUsers = true;
                    options.Lockout.MaxFailedAccessAttempts = 5;
                    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                })
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();
            services.AddScoped<IUserValidator<IdentityUser>, OptionalUniqueEmailUserValidator>();
            services.Configure<EmailSettings>(options =>
                options.ClientUrl = "https://client.example.test");
            services.Configure<JwtSettings>(options =>
            {
                options.PrivateKey = privateKey;
                options.PublicKey = publicKey;
            });
            services.Configure<AuthBootstrapSettings>(options =>
                options.AdminPassword = InitialPassword);
            services.AddSingleton<IAccountEmailSender>(
                new TestAccountEmailSender(failEmailConfirmation));
            services.AddSingleton<IWebHostEnvironment>(
                new TestWebHostEnvironment(contentRootPath));
            services.AddSingleton<CertificateFileStore>();
            services.AddSingleton<LoginTimingEqualizer>();
            services.AddScoped<JwtService>();
            services.AddScoped<AccountSecurityService>();
            services.AddScoped<AccountDeletionService>();
            services.AddScoped<AuthManagerRepository>();

            var provider = services.BuildServiceProvider();
            var scope = provider.CreateAsyncScope();
            scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>()
                .Database
                .EnsureCreated();
            return new LoginTestHost(provider, scope, connection, contentRootPath);
        }

        internal async Task<IdentityUser> CreateUserAsync(
            string userName,
            string email,
            bool emailConfirmed)
        {
            var user = new IdentityUser
            {
                UserName = userName,
                Email = email,
                EmailConfirmed = emailConfirmed
            };
            var result = await UserManager.CreateAsync(user, InitialPassword);
            Assert.True(
                result.Succeeded,
                string.Join(", ", result.Errors.Select(error => error.Description)));
            return user;
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

    private sealed class TestAccountEmailSender(bool failConfirmation) : IAccountEmailSender
    {
        internal const string FailureMessage = "Simulated email confirmation failure.";

        public Task SendConfirmationLinkAsync(
            IdentityUser user,
            string email,
            string confirmationLink,
            CancellationToken cancellationToken = default) => failConfirmation
                ? Task.FromException(new InvalidOperationException(FailureMessage))
                : Task.CompletedTask;

        public Task SendPasswordResetLinkAsync(
            IdentityUser user,
            string email,
            string resetLink,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SendEmailChangeLinkAsync(
            IdentityUser user,
            string newEmail,
            string confirmationLink,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SendEmailChangedNotificationAsync(
            IdentityUser user,
            string previousEmail,
            string newEmail,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class TestWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = typeof(AuthManagerRepositoryLoginTests).Assembly.FullName!;

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string WebRootPath { get; set; } = contentRootPath;

        public string EnvironmentName { get; set; } = Environments.Development;

        public string ContentRootPath { get; set; } = contentRootPath;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
