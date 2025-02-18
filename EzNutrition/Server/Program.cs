using EzNutrition.Server.Data;
using EzNutrition.Server.Data.Repositories;
using EzNutrition.Server.Policies;
using EzNutrition.Server.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Security.Cryptography;

namespace EzNutrition
{
    public class Program
    {

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            // Add services to the container.
            builder.Services.AddDbContext<EzNutritionDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("EzNutritionDB")));
            builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("ApplicationDb")));
            #region Identity and Auth
            //Identity and Auth
            builder.Services.AddIdentity<IdentityUser, IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
            builder.Services.AddAuthorization(PolicyList.RegisterPolicies);

            var publicKeyBytes = Convert.FromBase64String(builder.Configuration.GetSection("PublicKey").Value);
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
            #endregion
            builder.Services.AddControllersWithViews();
            builder.Services.AddRazorPages();
            builder.Services.AddTransient<JwtService>();
            builder.Services.AddTransient<DiseaseRepository>();
            builder.Services.AddTransient<AdviceRepository>();
            builder.Services.AddTransient<DietaryReferenceIntakeRepository>();
            builder.Services.AddTransient<AuthManagerRepository>();
            builder.Services.AddTransient<FoodNutritionValueRepository>();


            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var nutrDb = scope.ServiceProvider.GetRequiredService<EzNutritionDbContext>();
                nutrDb.Database.Migrate();
                var appDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                appDb.Database.Migrate();
                if (args.Any(x => x == "AuthInitialize"))
                {
                    var auth = scope.ServiceProvider.GetRequiredService<AuthManagerRepository>();
                    auth.Initialize().Wait();
                }
            }

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseWebAssemblyDebugging();
            }
            else
            {
                app.UseExceptionHandler("/Error");
            }

            app.UseBlazorFrameworkFiles();
            app.UseStaticFiles();

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapRazorPages();
            app.MapControllers();
            app.MapFallbackToFile("index.html");

            app.Run();
        }
    }
}