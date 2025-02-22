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
            var publicKeyBytes = Convert.FromBase64String(builder.Configuration["JwtSettings:PublicKey"]!);
            var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);
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