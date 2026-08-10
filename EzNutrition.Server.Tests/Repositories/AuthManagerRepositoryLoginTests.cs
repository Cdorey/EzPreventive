using EzNutrition.Server.Data;
using EzNutrition.Server.Data.Repositories;
using EzNutrition.Server.Services;
using EzNutrition.Server.Services.Settings;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

        private LoginTestHost(ServiceProvider rootProvider, AsyncServiceScope scope)
        {
            this.rootProvider = rootProvider;
            this.scope = scope;
            UserManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            Repository = scope.ServiceProvider.GetRequiredService<AuthManagerRepository>();
        }

        internal UserManager<IdentityUser> UserManager { get; }

        internal AuthManagerRepository Repository { get; }

        internal static LoginTestHost Create()
        {
            using var rsa = RSA.Create(2048);
            var privateKey = Convert.ToBase64String(rsa.ExportPkcs8PrivateKey());
            var publicKey = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
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
            services.AddSingleton<IAccountEmailSender, NullAccountEmailSender>();
            services.AddSingleton<LoginTimingEqualizer>();
            services.AddScoped<JwtService>();
            services.AddScoped<AccountSecurityService>();
            services.AddScoped<AuthManagerRepository>();

            var provider = services.BuildServiceProvider();
            return new LoginTestHost(provider, provider.CreateAsyncScope());
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
        }
    }

    private sealed class NullAccountEmailSender : IAccountEmailSender
    {
        public Task SendConfirmationLinkAsync(
            IdentityUser user,
            string email,
            string confirmationLink,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

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
}
