using EzNutrition.Server.Data;
using EzNutrition.Shared.Policies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;

namespace EzNutrition.Server.Extension
{
    internal static class ProgramExtension
    {
        internal static void AuthorizeConfiguration(this WebApplicationBuilder builder)
        {
            //Identity and Auth
            builder.Services.AddIdentity<IdentityUser, IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
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
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = "EzPreventive",
                    ValidAudience = "EzNutrition",
                    IssuerSigningKey = new RsaSecurityKey(rsa)
                };
            });
            builder.Services.Configure<IdentityOptions>(options =>
            {
                options.Password.RequireDigit = false;
                options.Password.RequiredLength = 6;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
            });
        }
    }
}
