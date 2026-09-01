using EzNutrition.Server.Data;
using EzNutrition.Server.Services;
using EzNutrition.Shared.Policies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace EzNutrition.Server.Extension
{
    internal static class ProgramExtension
    {
        internal static void AuthorizeConfiguration(this WebApplicationBuilder builder)
        {
            //Identity and Auth
            builder.Services
                .AddIdentity<IdentityUser, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();
            builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
            {
                options.TokenLifespan = TimeSpan.FromHours(3);
            });
            builder.Services.AddScoped<IUserValidator<IdentityUser>, OptionalUniqueEmailUserValidator>();
            builder.Services.AddAuthorization(PolicyList.RegisterPolicies);
            JwtSecurityTokenHandler.DefaultOutboundClaimTypeMap.Clear();
            var publicKey = builder.Configuration[
                $"{nameof(EzNutrition.Server.Services.Settings.JwtSettings)}:{nameof(EzNutrition.Server.Services.Settings.JwtSettings.PublicKey)}"];
            if (string.IsNullOrWhiteSpace(publicKey))
            {
                throw new InvalidOperationException("JwtSettings:PublicKey is missing.");
            }

            var rsa = RSA.Create();
            try
            {
                var publicKeyBytes = Convert.FromBase64String(publicKey);
                rsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);
            }
            catch (Exception ex) when (ex is FormatException or CryptographicException)
            {
                rsa.Dispose();
                throw new InvalidOperationException("JwtSettings:PublicKey is not a valid Base64-encoded RSA public key.", ex);
            }
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = "EzPreventive",
                    ValidAudience = "EzNutrition",
                    IssuerSigningKey = new RsaSecurityKey(rsa),
                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                            ?? context.Principal?.FindFirstValue(ClaimTypes.Upn);
                        var fingerprint = context.Principal?.FindFirstValue(JwtService.SecurityStampClaimType);
                        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(fingerprint))
                        {
                            context.Fail("The access token does not contain the required account version.");
                            return;
                        }

                        var userManager = context.HttpContext.RequestServices
                            .GetRequiredService<UserManager<IdentityUser>>();
                        var user = await userManager.FindByIdAsync(userId);
                        if (user is null)
                        {
                            context.Fail("The account no longer exists.");
                            return;
                        }

                        if (userManager.SupportsUserLockout && await userManager.IsLockedOutAsync(user))
                        {
                            context.Fail("The account is locked.");
                            return;
                        }

                        var currentStamp = await userManager.GetSecurityStampAsync(user);
                        if (string.IsNullOrWhiteSpace(currentStamp) ||
                            !JwtService.IsSecurityStampFingerprintValid(fingerprint, currentStamp))
                        {
                            context.Fail("The access token is no longer valid for this account.");
                        }
                    }
                };
            });
            builder.Services.Configure<IdentityOptions>(options =>
            {
                options.Password.RequireDigit = false;
                options.Password.RequiredLength = 6;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            });
        }
    }
}
