using EzNutrition.Server.Data;
using EzNutrition.Server.Services;
using EzNutrition.Server.Services.Settings;
using EzNutrition.Shared.Data.DTO;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Text;

namespace EzNutrition.Server.Tests.Services;

public sealed class AccountSecurityServiceTests
{
    [Fact]
    public async Task Resend_confirmation_sends_only_for_one_unconfirmed_account()
    {
        await using var host = TestHost.Create();
        var user = await host.CreateUserAsync("pending", "pending@example.test", confirmed: false);

        await host.Service.RequestEmailConfirmationAsync("missing@example.test");
        Assert.Empty(host.EmailSender.Messages);

        await host.Service.RequestEmailConfirmationAsync("PENDING@example.test");

        var message = Assert.Single(host.EmailSender.Messages);
        Assert.Equal(TestEmailKind.Confirmation, message.Kind);
        Assert.Equal(user.Id, message.UserId);
        var query = ParseQuery(message.Link);
        Assert.Equal(user.Id, query["userId"]);
        Assert.False(string.IsNullOrWhiteSpace(query["token"]));

        host.EmailSender.Clear();
        user.EmailConfirmed = true;
        Assert.True((await host.UserManager.UpdateAsync(user)).Succeeded);

        await host.Service.RequestEmailConfirmationAsync(user.Email!);

        Assert.Empty(host.EmailSender.Messages);
    }

    [Fact]
    public async Task Email_confirmation_link_round_trips_and_stops_further_resends()
    {
        await using var host = TestHost.Create();
        var user = await host.CreateUserAsync(
            "confirmation-round-trip",
            "confirmation@example.test",
            confirmed: false);

        await host.Service.RequestEmailConfirmationAsync(user.Email!);

        var email = Assert.Single(host.EmailSender.Messages);
        Assert.Equal(TestEmailKind.Confirmation, email.Kind);
        var query = ParseQuery(email.Link);

        var confirmed = await host.Service.ConfirmEmailAsync(
            query["userId"],
            query["token"]);

        Assert.True(confirmed.Succeeded, confirmed.Message);
        Assert.True(await host.UserManager.IsEmailConfirmedAsync(user));

        host.EmailSender.Clear();
        await host.Service.RequestEmailConfirmationAsync(user.Email!);
        Assert.Empty(host.EmailSender.Messages);
    }

    [Fact]
    public async Task Unconfirmed_email_cannot_request_a_password_reset()
    {
        await using var host = TestHost.Create();
        var user = await host.CreateUserAsync(
            "unconfirmed-recovery",
            "unconfirmed@example.test",
            confirmed: false);

        await host.Service.RequestPasswordResetAsync(user.Email!);

        Assert.Empty(host.EmailSender.Messages);
        Assert.False(await host.UserManager.IsEmailConfirmedAsync(user));
    }

    [Fact]
    public async Task Recovery_requests_fail_closed_when_email_is_duplicated()
    {
        await using var host = TestHost.Create(enforceUniqueEmail: false);
        await host.CreateUserAsync("first", "duplicate@example.test", confirmed: true);
        await host.CreateUserAsync("second", "duplicate@example.test", confirmed: true);

        await host.Service.RequestEmailConfirmationAsync("duplicate@example.test");
        await host.Service.RequestPasswordResetAsync("duplicate@example.test");

        Assert.Empty(host.EmailSender.Messages);
    }

    [Fact]
    public async Task Password_reset_link_round_trips_and_cannot_be_reused()
    {
        await using var host = TestHost.Create();
        var user = await host.CreateUserAsync("recover", "recover@example.test", confirmed: true);
        var previousStamp = await host.UserManager.GetSecurityStampAsync(user);

        await host.Service.RequestPasswordResetAsync(user.Email!);

        var email = Assert.Single(host.EmailSender.Messages);
        Assert.Equal(TestEmailKind.PasswordReset, email.Kind);
        var query = ParseQuery(email.Link);
        var request = new ResetPasswordDto
        {
            UserId = query["userId"],
            Token = query["token"],
            NewPassword = "new-password",
            ConfirmPassword = "new-password"
        };

        var result = await host.Service.ResetPasswordAsync(request);

        Assert.True(result.Succeeded, result.Message);
        Assert.False(await host.UserManager.CheckPasswordAsync(user, TestHost.InitialPassword));
        Assert.True(await host.UserManager.CheckPasswordAsync(user, request.NewPassword));
        Assert.NotEqual(previousStamp, await host.UserManager.GetSecurityStampAsync(user));

        var replay = await host.Service.ResetPasswordAsync(request);
        Assert.False(replay.Succeeded);
    }

    [Fact]
    public async Task Change_password_requires_current_password_and_rotates_stamp()
    {
        await using var host = TestHost.Create();
        var user = await host.CreateUserAsync("password", "password@example.test", confirmed: true);
        var previousStamp = await host.UserManager.GetSecurityStampAsync(user);

        var rejected = await host.Service.ChangePasswordAsync(
            user.Id,
            new ChangePasswordDto
            {
                CurrentPassword = "wrong-password",
                NewPassword = "replacement",
                ConfirmPassword = "replacement"
            });
        Assert.False(rejected.Succeeded);

        var accepted = await host.Service.ChangePasswordAsync(
            user.Id,
            new ChangePasswordDto
            {
                CurrentPassword = TestHost.InitialPassword,
                NewPassword = "replacement",
                ConfirmPassword = "replacement"
            });

        Assert.True(accepted.Succeeded, accepted.Message);
        Assert.True(await host.UserManager.CheckPasswordAsync(user, "replacement"));
        Assert.NotEqual(previousStamp, await host.UserManager.GetSecurityStampAsync(user));
    }

    [Fact]
    public async Task Email_change_keeps_old_email_until_link_is_confirmed()
    {
        await using var host = TestHost.Create();
        var user = await host.CreateUserAsync("email", "old@example.test", confirmed: true);
        var previousStamp = await host.UserManager.GetSecurityStampAsync(user);

        var requested = await host.Service.RequestEmailChangeAsync(
            user.Id,
            new RequestEmailChangeDto
            {
                CurrentPassword = TestHost.InitialPassword,
                NewEmail = "new@example.test"
            });

        Assert.True(requested.Succeeded, requested.Message);
        Assert.Equal("old@example.test", user.Email);
        var email = Assert.Single(host.EmailSender.Messages);
        Assert.Equal(TestEmailKind.EmailChange, email.Kind);
        Assert.Equal("new@example.test", email.Recipient);
        var query = ParseQuery(email.Link);

        var confirmed = await host.Service.ConfirmEmailChangeAsync(
            new ConfirmEmailChangeDto
            {
                UserId = query["userId"],
                NewEmail = query["newEmail"],
                Token = query["token"]
            });

        Assert.True(confirmed.Succeeded, confirmed.Message);
        Assert.Equal("new@example.test", user.Email);
        Assert.True(user.EmailConfirmed);
        Assert.NotEqual(previousStamp, await host.UserManager.GetSecurityStampAsync(user));
        var notification = Assert.Single(
            host.EmailSender.Messages,
            item => item.Kind == TestEmailKind.EmailChangedNotification);
        Assert.Equal("old@example.test", notification.Recipient);
    }

    [Fact]
    public async Task Email_change_rejects_wrong_password_and_an_occupied_email()
    {
        await using var host = TestHost.Create();
        var user = await host.CreateUserAsync("owner", "owner@example.test", confirmed: true);
        await host.CreateUserAsync("occupied", "occupied@example.test", confirmed: true);

        var wrongPassword = await host.Service.RequestEmailChangeAsync(
            user.Id,
            new RequestEmailChangeDto
            {
                CurrentPassword = "wrong",
                NewEmail = "available@example.test"
            });
        var duplicate = await host.Service.RequestEmailChangeAsync(
            user.Id,
            new RequestEmailChangeDto
            {
                CurrentPassword = TestHost.InitialPassword,
                NewEmail = "OCCUPIED@example.test"
            });

        Assert.False(wrongPassword.Succeeded);
        Assert.False(duplicate.Succeeded);
        Assert.Empty(host.EmailSender.Messages);
        Assert.Equal("owner@example.test", user.Email);
    }

    [Fact]
    public async Task Email_change_confirmation_rechecks_an_address_claimed_after_request()
    {
        await using var host = TestHost.Create();
        var user = await host.CreateUserAsync("owner", "owner@example.test", confirmed: true);
        var requested = await host.Service.RequestEmailChangeAsync(
            user.Id,
            new RequestEmailChangeDto
            {
                CurrentPassword = TestHost.InitialPassword,
                NewEmail = "contended@example.test"
            });
        Assert.True(requested.Succeeded, requested.Message);
        var query = ParseQuery(Assert.Single(host.EmailSender.Messages).Link);
        await host.CreateUserAsync("contender", "contended@example.test", confirmed: true);

        var confirmed = await host.Service.ConfirmEmailChangeAsync(
            new ConfirmEmailChangeDto
            {
                UserId = query["userId"],
                NewEmail = query["newEmail"],
                Token = query["token"]
            });

        Assert.False(confirmed.Succeeded);
        Assert.Equal("owner@example.test", user.Email);
    }

    [Fact]
    public async Task Invalid_email_change_token_has_the_same_result_for_available_and_occupied_addresses()
    {
        await using var host = TestHost.Create();
        var user = await host.CreateUserAsync("token-owner", "owner@example.test", confirmed: true);
        await host.CreateUserAsync("token-occupant", "occupied@example.test", confirmed: true);
        var originalStamp = await host.UserManager.GetSecurityStampAsync(user);
        var invalidToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes("invalid-token"));

        var availableResult = await host.Service.ConfirmEmailChangeAsync(
            new ConfirmEmailChangeDto
            {
                UserId = user.Id,
                NewEmail = "available@example.test",
                Token = invalidToken
            });
        var occupiedResult = await host.Service.ConfirmEmailChangeAsync(
            new ConfirmEmailChangeDto
            {
                UserId = user.Id,
                NewEmail = "occupied@example.test",
                Token = invalidToken
            });

        Assert.False(availableResult.Succeeded);
        Assert.False(occupiedResult.Succeeded);
        Assert.Equal(availableResult.Message, occupiedResult.Message);
        Assert.Equal("owner@example.test", user.Email);
        Assert.True(user.EmailConfirmed);
        Assert.Equal(originalStamp, await host.UserManager.GetSecurityStampAsync(user));
        Assert.Empty(host.EmailSender.Messages);
    }

    [Fact]
    public async Task Email_change_rolls_back_relational_write_when_security_stamp_update_fails()
    {
        await using var host = TestHost.CreateSqlite();
        var user = await host.CreateUserAsync(
            "email-rollback",
            "original@example.test",
            confirmed: false);
        user.PhoneNumber = "+86 13800000000";
        user.PhoneNumberConfirmed = true;
        Assert.True((await host.UserManager.UpdateAsync(user)).Succeeded);
        var originalEmail = user.Email;
        var originalNormalizedEmail = user.NormalizedEmail;
        var originalEmailConfirmed = user.EmailConfirmed;
        var originalPhoneNumber = user.PhoneNumber;
        var originalPhoneConfirmed = user.PhoneNumberConfirmed;
        var originalSecurityStamp = await host.UserManager.GetSecurityStampAsync(user);

        var requested = await host.Service.RequestEmailChangeAsync(
            user.Id,
            new RequestEmailChangeDto
            {
                CurrentPassword = TestHost.InitialPassword,
                NewEmail = "replacement@example.test"
            });
        Assert.True(requested.Succeeded, requested.Message);
        var query = ParseQuery(Assert.Single(host.EmailSender.Messages).Link);
        host.FailValidationOnCall(2);

        var result = await host.Service.ConfirmEmailChangeAsync(
            new ConfirmEmailChangeDto
            {
                UserId = query["userId"],
                NewEmail = query["newEmail"],
                Token = query["token"]
            });

        Assert.False(result.Succeeded);
        Assert.Equal(2, host.ValidationCallCount);
        Assert.Equal(originalEmail, user.Email);
        Assert.Equal(originalNormalizedEmail, user.NormalizedEmail);
        Assert.Equal(originalEmailConfirmed, user.EmailConfirmed);
        Assert.Equal(originalPhoneNumber, user.PhoneNumber);
        Assert.Equal(originalPhoneConfirmed, user.PhoneNumberConfirmed);
        Assert.Equal(originalSecurityStamp, await host.UserManager.GetSecurityStampAsync(user));
        Assert.DoesNotContain(
            host.EmailSender.Messages,
            message => message.Kind == TestEmailKind.EmailChangedNotification);

        var persisted = await host.LoadUserFromFreshScopeAsync(user.Id);
        Assert.NotNull(persisted);
        Assert.Equal(originalEmail, persisted.Email);
        Assert.Equal(originalNormalizedEmail, persisted.NormalizedEmail);
        Assert.Equal(originalEmailConfirmed, persisted.EmailConfirmed);
        Assert.Equal(originalPhoneNumber, persisted.PhoneNumber);
        Assert.Equal(originalPhoneConfirmed, persisted.PhoneNumberConfirmed);
        Assert.Equal(originalSecurityStamp, persisted.SecurityStamp);
    }

    [Fact]
    public async Task Phone_change_requires_password_and_resets_confirmation()
    {
        await using var host = TestHost.Create();
        var user = await host.CreateUserAsync("phone", "phone@example.test", confirmed: true);
        user.PhoneNumber = "+86 13800000000";
        user.PhoneNumberConfirmed = true;
        Assert.True((await host.UserManager.UpdateAsync(user)).Succeeded);
        var previousStamp = await host.UserManager.GetSecurityStampAsync(user);

        var rejected = await host.Service.ChangePhoneNumberAsync(
            user.Id,
            new ChangePhoneNumberDto
            {
                CurrentPassword = "wrong",
                PhoneNumber = "+86 13900000000"
            });
        Assert.False(rejected.Succeeded);
        Assert.Equal("+86 13800000000", user.PhoneNumber);

        var accepted = await host.Service.ChangePhoneNumberAsync(
            user.Id,
            new ChangePhoneNumberDto
            {
                CurrentPassword = TestHost.InitialPassword,
                PhoneNumber = "+86 13900000000"
            });

        Assert.True(accepted.Succeeded, accepted.Message);
        Assert.Equal("+86 13900000000", user.PhoneNumber);
        Assert.False(user.PhoneNumberConfirmed);
        Assert.NotEqual(previousStamp, await host.UserManager.GetSecurityStampAsync(user));
    }

    [Fact]
    public async Task Phone_change_rolls_back_relational_write_when_security_stamp_update_fails()
    {
        await using var host = TestHost.CreateSqlite();
        var user = await host.CreateUserAsync(
            "phone-rollback",
            "phone-rollback@example.test",
            confirmed: true);
        user.PhoneNumber = "+86 13800000000";
        user.PhoneNumberConfirmed = true;
        Assert.True((await host.UserManager.UpdateAsync(user)).Succeeded);
        var originalEmail = user.Email;
        var originalNormalizedEmail = user.NormalizedEmail;
        var originalEmailConfirmed = user.EmailConfirmed;
        var originalPhoneNumber = user.PhoneNumber;
        var originalPhoneConfirmed = user.PhoneNumberConfirmed;
        var originalSecurityStamp = await host.UserManager.GetSecurityStampAsync(user);
        host.FailValidationOnCall(2);

        var result = await host.Service.ChangePhoneNumberAsync(
            user.Id,
            new ChangePhoneNumberDto
            {
                CurrentPassword = TestHost.InitialPassword,
                PhoneNumber = "+86 13900000000"
            });

        Assert.False(result.Succeeded);
        Assert.Equal(2, host.ValidationCallCount);
        Assert.Equal(originalEmail, user.Email);
        Assert.Equal(originalNormalizedEmail, user.NormalizedEmail);
        Assert.Equal(originalEmailConfirmed, user.EmailConfirmed);
        Assert.Equal(originalPhoneNumber, user.PhoneNumber);
        Assert.Equal(originalPhoneConfirmed, user.PhoneNumberConfirmed);
        Assert.Equal(originalSecurityStamp, await host.UserManager.GetSecurityStampAsync(user));

        var persisted = await host.LoadUserFromFreshScopeAsync(user.Id);
        Assert.NotNull(persisted);
        Assert.Equal(originalEmail, persisted.Email);
        Assert.Equal(originalNormalizedEmail, persisted.NormalizedEmail);
        Assert.Equal(originalEmailConfirmed, persisted.EmailConfirmed);
        Assert.Equal(originalPhoneNumber, persisted.PhoneNumber);
        Assert.Equal(originalPhoneConfirmed, persisted.PhoneNumberConfirmed);
        Assert.Equal(originalSecurityStamp, persisted.SecurityStamp);
    }

    [Fact]
    public async Task Security_stamp_fingerprint_rejects_a_rotated_stamp()
    {
        await using var host = TestHost.Create();
        var user = await host.CreateUserAsync("jwt", "jwt@example.test", confirmed: true);
        var originalStamp = await host.UserManager.GetSecurityStampAsync(user);
        var fingerprint = JwtService.CreateSecurityStampFingerprint(originalStamp);

        Assert.True(JwtService.IsSecurityStampFingerprintValid(fingerprint, originalStamp));

        Assert.True((await host.UserManager.UpdateSecurityStampAsync(user)).Succeeded);
        var rotatedStamp = await host.UserManager.GetSecurityStampAsync(user);

        Assert.False(JwtService.IsSecurityStampFingerprintValid(fingerprint, rotatedStamp));
    }

    [Theory]
    [InlineData("role")]
    [InlineData("roles")]
    [InlineData("upn")]
    [InlineData("nameid")]
    [InlineData("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")]
    [InlineData("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")]
    public void Jwt_reserved_claim_filter_covers_short_and_uri_identity_claims(string claimType)
    {
        Assert.True(JwtService.IsReservedClaimType(claimType));
    }

    [Fact]
    public async Task Optional_unique_email_validation_allows_email_less_bootstrap_users()
    {
        await using var host = TestHost.Create();
        var admin = new IdentityUser { UserName = "Admin" };

        var adminResult = await host.UserManager.CreateAsync(admin, TestHost.InitialPassword);
        await host.CreateUserAsync("first", "unique@example.test", confirmed: true);
        var duplicateResult = await host.UserManager.CreateAsync(
            new IdentityUser
            {
                UserName = "second",
                Email = "UNIQUE@example.test"
            },
            TestHost.InitialPassword);

        Assert.True(
            adminResult.Succeeded,
            string.Join(", ", adminResult.Errors.Select(error => error.Description)));
        Assert.False(duplicateResult.Succeeded);
        Assert.Contains(duplicateResult.Errors, error => error.Code == nameof(IdentityErrorDescriber.DuplicateEmail));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("   ")]
    public async Task Optional_unique_email_validation_rejects_invalid_non_empty_addresses(string email)
    {
        await using var host = TestHost.Create();
        var result = await host.UserManager.CreateAsync(
            new IdentityUser { UserName = $"invalid-{Guid.NewGuid():N}", Email = email },
            TestHost.InitialPassword);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == nameof(IdentityErrorDescriber.InvalidEmail));
    }

    private static IReadOnlyDictionary<string, string> ParseQuery(string? link)
    {
        Assert.False(string.IsNullOrWhiteSpace(link));
        return QueryHelpers.ParseQuery(new Uri(link).Query)
            .ToDictionary(pair => pair.Key, pair => pair.Value.ToString());
    }

    private sealed class TestHost : IAsyncDisposable
    {
        internal const string InitialPassword = "initial-password";

        private readonly ServiceProvider rootProvider;
        private readonly AsyncServiceScope scope;
        private readonly SqliteConnection? sqliteConnection;
        private readonly NthCallFailingUserValidator? failingValidator;

        private TestHost(
            ServiceProvider rootProvider,
            AsyncServiceScope scope,
            FakeAccountEmailSender emailSender,
            SqliteConnection? sqliteConnection,
            NthCallFailingUserValidator? failingValidator)
        {
            this.rootProvider = rootProvider;
            this.scope = scope;
            this.sqliteConnection = sqliteConnection;
            this.failingValidator = failingValidator;
            EmailSender = emailSender;
            UserManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            Service = scope.ServiceProvider.GetRequiredService<AccountSecurityService>();
        }

        internal UserManager<IdentityUser> UserManager { get; }

        internal AccountSecurityService Service { get; }

        internal FakeAccountEmailSender EmailSender { get; }

        internal int ValidationCallCount => failingValidator?.CallCount
            ?? throw new InvalidOperationException("This host has no controllable validator.");

        internal static TestHost Create(bool enforceUniqueEmail = true) =>
            CreateCore(
                enforceUniqueEmail,
                options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()),
                sqliteConnection: null,
                failingValidator: null);

        internal static TestHost CreateSqlite()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            var validator = new NthCallFailingUserValidator();
            return CreateCore(
                enforceUniqueEmail: true,
                options => options.UseSqlite(connection),
                connection,
                validator);
        }

        private static TestHost CreateCore(
            bool enforceUniqueEmail,
            Action<DbContextOptionsBuilder> configureDatabase,
            SqliteConnection? sqliteConnection,
            NthCallFailingUserValidator? failingValidator)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.AddDbContext<ApplicationDbContext>(configureDatabase);
            services
                .AddIdentity<IdentityUser, IdentityRole>(options =>
                {
                    options.Password.RequireDigit = false;
                    options.Password.RequiredLength = 6;
                    options.Password.RequireNonAlphanumeric = false;
                    options.Password.RequireUppercase = false;
                    options.Password.RequireLowercase = false;
                })
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();
            if (enforceUniqueEmail)
            {
                services.AddScoped<IUserValidator<IdentityUser>, OptionalUniqueEmailUserValidator>();
            }
            if (failingValidator is not null)
            {
                services.AddSingleton<IUserValidator<IdentityUser>>(failingValidator);
            }
            services.Configure<EmailSettings>(options =>
                options.ClientUrl = "https://client.example.test");
            var emailSender = new FakeAccountEmailSender();
            services.AddSingleton<IAccountEmailSender>(emailSender);
            services.AddScoped<AccountSecurityService>();

            var provider = services.BuildServiceProvider();
            var scope = provider.CreateAsyncScope();
            if (sqliteConnection is not null)
            {
                scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>()
                    .Database
                    .EnsureCreated();
            }

            return new TestHost(
                provider,
                scope,
                emailSender,
                sqliteConnection,
                failingValidator);
        }

        internal void FailValidationOnCall(int callNumber) =>
            (failingValidator ?? throw new InvalidOperationException(
                "This host has no controllable validator."))
            .FailOnCall(callNumber);

        internal async Task<IdentityUser> CreateUserAsync(
            string userName,
            string email,
            bool confirmed)
        {
            var user = new IdentityUser
            {
                UserName = userName,
                Email = email,
                EmailConfirmed = confirmed
            };
            var result = await UserManager.CreateAsync(user, InitialPassword);
            Assert.True(
                result.Succeeded,
                string.Join(", ", result.Errors.Select(error => error.Description)));
            return user;
        }

        internal async Task<IdentityUser?> LoadUserFromFreshScopeAsync(string userId)
        {
            await using var verificationScope = rootProvider.CreateAsyncScope();
            var verificationManager = verificationScope.ServiceProvider
                .GetRequiredService<UserManager<IdentityUser>>();
            return await verificationManager.FindByIdAsync(userId);
        }

        public async ValueTask DisposeAsync()
        {
            await scope.DisposeAsync();
            await rootProvider.DisposeAsync();
            if (sqliteConnection is not null)
            {
                await sqliteConnection.DisposeAsync();
            }
        }
    }

    private sealed class NthCallFailingUserValidator : IUserValidator<IdentityUser>
    {
        private int callCount;
        private int failOnCall = -1;

        internal int CallCount => Volatile.Read(ref callCount);

        internal void FailOnCall(int callNumber)
        {
            if (callNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(callNumber));
            }

            Volatile.Write(ref callCount, 0);
            Volatile.Write(ref failOnCall, callNumber);
        }

        public Task<IdentityResult> ValidateAsync(
            UserManager<IdentityUser> manager,
            IdentityUser user)
        {
            var currentCall = Interlocked.Increment(ref callCount);
            var result = currentCall == Volatile.Read(ref failOnCall)
                ? IdentityResult.Failed(new IdentityError
                {
                    Code = "InjectedValidationFailure",
                    Description = "Injected validation failure for transaction rollback testing."
                })
                : IdentityResult.Success;
            return Task.FromResult(result);
        }
    }

    private sealed class FakeAccountEmailSender : IAccountEmailSender
    {
        private readonly ConcurrentQueue<TestEmail> messages = new();

        internal IReadOnlyCollection<TestEmail> Messages => messages.ToArray();

        public Task SendConfirmationLinkAsync(
            IdentityUser user,
            string email,
            string confirmationLink,
            CancellationToken cancellationToken = default)
        {
            messages.Enqueue(new TestEmail(TestEmailKind.Confirmation, user.Id, email, confirmationLink));
            return Task.CompletedTask;
        }

        public Task SendPasswordResetLinkAsync(
            IdentityUser user,
            string email,
            string resetLink,
            CancellationToken cancellationToken = default)
        {
            messages.Enqueue(new TestEmail(TestEmailKind.PasswordReset, user.Id, email, resetLink));
            return Task.CompletedTask;
        }

        public Task SendEmailChangeLinkAsync(
            IdentityUser user,
            string newEmail,
            string confirmationLink,
            CancellationToken cancellationToken = default)
        {
            messages.Enqueue(new TestEmail(TestEmailKind.EmailChange, user.Id, newEmail, confirmationLink));
            return Task.CompletedTask;
        }

        public Task SendEmailChangedNotificationAsync(
            IdentityUser user,
            string previousEmail,
            string newEmail,
            CancellationToken cancellationToken = default)
        {
            messages.Enqueue(new TestEmail(
                TestEmailKind.EmailChangedNotification,
                user.Id,
                previousEmail,
                Link: null));
            return Task.CompletedTask;
        }

        internal void Clear()
        {
            while (messages.TryDequeue(out _))
            {
            }
        }
    }

    private sealed record TestEmail(
        TestEmailKind Kind,
        string UserId,
        string Recipient,
        string? Link);

    private enum TestEmailKind
    {
        Confirmation,
        PasswordReset,
        EmailChange,
        EmailChangedNotification
    }
}
