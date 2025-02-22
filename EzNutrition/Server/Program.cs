using EzNutrition.Server.Data;
using EzNutrition.Server.Data.Repositories;
using EzNutrition.Server.Extension;
using EzNutrition.Server.Services;
using EzNutrition.Server.Services.Settings;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace EzNutrition.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            // Add services to the container.
            builder.Services.AddDbContext<EzNutritionDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("EzNutritionDB")));
            builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("ApplicationDb")));
            builder.AuthorizeConfiguration();
            builder.Services.AddControllersWithViews();
            builder.Services.AddRazorPages();
            builder.Services.AddTransient<JwtService>();
            builder.Services.AddTransient<DiseaseRepository>();
            builder.Services.AddTransient<AdviceRepository>();
            builder.Services.AddTransient<DietaryReferenceIntakeRepository>();
            builder.Services.AddTransient<AuthManagerRepository>();
            builder.Services.AddTransient<IEmailSender<IdentityUser>, SmtpEmailSender>();
            builder.Services.AddTransient<FoodNutritionValueRepository>();
            builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection(nameof(EmailSettings)));
            builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(nameof(JwtSettings)));


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