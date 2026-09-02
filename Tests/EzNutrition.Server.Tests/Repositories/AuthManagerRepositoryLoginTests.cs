using EzNutrition.Server.Controllers;
using EzNutrition.Server.Data;
using EzNutrition.Server.Data.Entities;
using EzNutrition.Server.Data.Repositories;
using EzNutrition.Server.Extension;
using EzNutrition.Server.Services;
using EzNutrition.Server.Services.Settings;
using EzNutrition.Shared.Data.DTO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;

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
        var utcNow = new DateTimeOffset(2026, 9, 2, 1, 2, 3, TimeSpan.Zero);
        await using var host = LoginTestHost.Create(timeProvider: new FixedTimeProvider(utcNow));

        await host.Repository.Initialize();

        var admin = await host.UserManager.FindByNameAsync("Admin");
        Assert.NotNull(admin);
        Assert.Null(admin.Email);
        Assert.False(admin.EmailConfirmed);
        Assert.Equal(utcNow.UtcDateTime, admin.CreatedAtUtc);
        Assert.Null(admin.LastSuccessfulLoginAtUtc);

        var accessToken = await host.Repository.Login(
            "Admin",
            LoginTestHost.InitialPassword);

        AssertJwt(accessToken);
        Assert.Equal(utcNow.UtcDateTime, admin.LastSuccessfulLoginAtUtc);
    }

    [Fact]
    public async Task Registration_and_successful_login_record_the_injected_utc_clock()
    {
        var utcNow = new DateTimeOffset(2026, 9, 2, 1, 2, 3, TimeSpan.Zero);
        await using var host = LoginTestHost.Create(timeProvider: new FixedTimeProvider(utcNow));
        var registration = new RegistrationDto
        {
            UserName = "lifecycle-user",
            Password = LoginTestHost.InitialPassword,
            Email = "lifecycle@example.test"
        };

        var registrationResult = await host.Repository.RegisterUserAsync(registration);

        Assert.True(registrationResult.Success);
        var user = await host.UserManager.FindByNameAsync(registration.UserName);
        Assert.NotNull(user);
        Assert.Equal(utcNow.UtcDateTime, user.CreatedAtUtc);
        Assert.Equal(DateTimeKind.Utc, user.CreatedAtUtc.Kind);
        Assert.Null(user.LastSuccessfulLoginAtUtc);

        var confirmationToken = await host.UserManager.GenerateEmailConfirmationTokenAsync(user);
        var confirmation = await host.UserManager.ConfirmEmailAsync(user, confirmationToken);
        Assert.True(confirmation.Succeeded);

        var accessToken = await host.Repository.Login(registration.UserName, registration.Password);

        AssertJwt(accessToken);
        host.DbContext.ChangeTracker.Clear();
        var persisted = await host.DbContext.Users.SingleAsync(candidate => candidate.Id == user.Id);
        Assert.Equal(utcNow.UtcDateTime, persisted.LastSuccessfulLoginAtUtc);
        Assert.Equal(DateTimeKind.Utc, persisted.LastSuccessfulLoginAtUtc?.Kind);
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

    [Fact]
    public async Task Certification_submission_and_review_use_the_injected_utc_clock()
    {
        var utcNow = new DateTimeOffset(2026, 9, 1, 2, 3, 4, TimeSpan.Zero);
        await using var host = LoginTestHost.Create(timeProvider: new FixedTimeProvider(utcNow));
        var registration = CreateProfessionalRegistration("utc-certification-user");

        var registrationResult = await host.Repository.RegisterUserAsync(registration);

        Assert.True(registrationResult.Success);
        var request = await host.DbContext.ProfessionalCertificationRequests.SingleAsync();
        Assert.Equal(utcNow.UtcDateTime, request.RequestTime);
        Assert.Equal(DateTimeKind.Utc, request.RequestTime.Kind);

        var review = request.ToDto();
        review.Status = RequestStatus.Approved;
        var controller = ActivatorUtilities.CreateInstance<AdminController>(host.Services);

        var actionResult = await controller.UpdateRequest(review, CancellationToken.None);

        Assert.IsType<OkObjectResult>(actionResult);
        host.DbContext.ChangeTracker.Clear();
        var reviewedRequest = await host.DbContext.ProfessionalCertificationRequests.SingleAsync();
        Assert.Equal(utcNow.UtcDateTime, reviewedRequest.ProcessedTime);
        Assert.Equal(DateTimeKind.Utc, reviewedRequest.ProcessedTime?.Kind);
    }

    [Fact]
    public async Task Notice_publication_uses_the_injected_utc_clock()
    {
        var utcNow = new DateTimeOffset(2026, 9, 1, 2, 3, 4, TimeSpan.Zero);
        await using var host = LoginTestHost.Create(timeProvider: new FixedTimeProvider(utcNow));
        var controller = ActivatorUtilities.CreateInstance<AdminController>(host.Services);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.Upn, "publisher@example.test")],
                    "test"))
            }
        };

        var actionResult = await controller.Notification(
            new NotificationDto
            {
                NoticeTitle = "UTC test",
                NoticeDescription = "UTC notice"
            },
            CancellationToken.None);

        Assert.IsType<OkResult>(actionResult);
        host.DbContext.ChangeTracker.Clear();
        var notice = await host.DbContext.Notices.SingleAsync();
        Assert.Equal(utcNow.UtcDateTime, notice.CreateTime);
        Assert.Equal(DateTimeKind.Utc, notice.CreateTime.Kind);
    }

    [Fact]
    public async Task Ai_audit_database_mapping_restores_utc_kind()
    {
        await using var host = LoginTestHost.Create();
        var requestTime = new DateTime(2026, 9, 1, 2, 3, 4, DateTimeKind.Unspecified);
        var processedTime = requestTime.AddMinutes(5);
        host.DbContext.PrescriptionGenerateRequests.Add(new PrescriptionGenerateRequest
        {
            Id = Guid.NewGuid(),
            UserId = "database-utc-user",
            Prompt = "test prompt",
            RequestTime = requestTime,
            ProcessedTime = processedTime
        });
        await host.DbContext.SaveChangesAsync();
        host.DbContext.ChangeTracker.Clear();

        var persisted = await host.DbContext.PrescriptionGenerateRequests.SingleAsync();

        Assert.Equal(requestTime, persisted.RequestTime);
        Assert.Equal(DateTimeKind.Utc, persisted.RequestTime.Kind);
        Assert.Equal(processedTime, persisted.ProcessedTime);
        Assert.Equal(DateTimeKind.Utc, persisted.ProcessedTime.Kind);
    }

    [Fact]
    public async Task Certification_database_mapping_restores_utc_kind()
    {
        await using var host = LoginTestHost.Create();
        var requestTime = new DateTime(2026, 9, 1, 2, 3, 4, DateTimeKind.Unspecified);
        var processedTime = requestTime.AddMinutes(30);
        host.DbContext.ProfessionalCertificationRequests.Add(new ProfessionalCertificationRequest
        {
            Id = Guid.NewGuid(),
            UserId = "database-utc-user",
            RequestTime = requestTime,
            IdentityType = "Physician",
            InstitutionName = "Test Institution",
            Status = RequestStatus.Approved,
            ProcessedTime = processedTime
        });
        await host.DbContext.SaveChangesAsync();
        host.DbContext.ChangeTracker.Clear();

        var persisted = await host.DbContext.ProfessionalCertificationRequests.SingleAsync();

        Assert.Equal(requestTime, persisted.RequestTime);
        Assert.Equal(DateTimeKind.Utc, persisted.RequestTime.Kind);
        Assert.Equal(processedTime, persisted.ProcessedTime);
        Assert.Equal(DateTimeKind.Utc, persisted.ProcessedTime?.Kind);
    }

    [Fact]
    public void Certification_http_contract_serializes_utc_designators()
    {
        var request = new ProfessionalCertificationRequest
        {
            Id = Guid.NewGuid(),
            UserId = "http-utc-user",
            RequestTime = new DateTime(2026, 9, 1, 2, 3, 4, DateTimeKind.Unspecified),
            IdentityType = "Physician",
            InstitutionName = "Test Institution",
            Status = RequestStatus.Approved,
            ProcessedTime = new DateTime(2026, 9, 1, 2, 33, 4, DateTimeKind.Unspecified)
        };

        var dto = request.ToDto();
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var json = JsonSerializer.Serialize(dto, jsonOptions);
        using var document = JsonDocument.Parse(json);
        var roundTrippedDto = JsonSerializer.Deserialize<ProfessionalCertificationRequestDto>(json, jsonOptions);

        Assert.Equal(DateTimeKind.Utc, dto.RequestTime.Kind);
        Assert.Equal(DateTimeKind.Utc, dto.ProcessedTime?.Kind);
        Assert.NotNull(roundTrippedDto);
        Assert.Equal(DateTimeKind.Utc, roundTrippedDto.RequestTime.Kind);
        Assert.Equal(DateTimeKind.Utc, roundTrippedDto.ProcessedTime?.Kind);
        Assert.EndsWith(
            "Z",
            document.RootElement.GetProperty("requestTime").GetString(),
            StringComparison.Ordinal);
        Assert.EndsWith(
            "Z",
            document.RootElement.GetProperty("processedTime").GetString(),
            StringComparison.Ordinal);
    }

    private static RegistrationDto CreateProfessionalRegistration(string userName) => new()
    {
        UserName = userName,
        Password = LoginTestHost.InitialPassword,
        Email = $"{userName}@example.test",
        ProfessionalIdentity = new ProfessionalIdentityDto
        {
            IdentityType = "Physician",
            InstitutionName = "Test Institution"
        }
    };

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
            UserManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Repository = scope.ServiceProvider.GetRequiredService<AuthManagerRepository>();
            DbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        }

        internal UserManager<ApplicationUser> UserManager { get; }

        internal AuthManagerRepository Repository { get; }

        internal ApplicationDbContext DbContext { get; }

        internal IServiceProvider Services => scope.ServiceProvider;

        internal static LoginTestHost Create(
            bool failEmailConfirmation = false,
            TimeProvider? timeProvider = null)
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
                .AddIdentity<ApplicationUser, IdentityRole>(options =>
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
            services.AddScoped<IUserValidator<ApplicationUser>, OptionalUniqueEmailUserValidator>();
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
            services.AddSingleton(timeProvider ?? TimeProvider.System);
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

        internal async Task<ApplicationUser> CreateUserAsync(
            string userName,
            string email,
            bool emailConfirmed)
        {
            var user = new ApplicationUser
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

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class TestAccountEmailSender(bool failConfirmation) : IAccountEmailSender
    {
        internal const string FailureMessage = "Simulated email confirmation failure.";

        public Task SendConfirmationLinkAsync(
            ApplicationUser user,
            string email,
            string confirmationLink,
            CancellationToken cancellationToken = default) => failConfirmation
                ? Task.FromException(new InvalidOperationException(FailureMessage))
                : Task.CompletedTask;

        public Task SendPasswordResetLinkAsync(
            ApplicationUser user,
            string email,
            string resetLink,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SendEmailChangeLinkAsync(
            ApplicationUser user,
            string newEmail,
            string confirmationLink,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SendEmailChangedNotificationAsync(
            ApplicationUser user,
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
