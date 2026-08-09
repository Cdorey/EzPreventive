using EzNutrition.AiAgency;
using EzNutrition.Server.Data;
using EzNutrition.Server.Data.Repositories;
using EzNutrition.Server.Extension;
using EzNutrition.Server.Services;
using EzNutrition.Server.Services.Settings;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;

namespace EzNutrition.Server
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            var nutritionConnectionString = builder.Configuration.GetConnectionString("EzNutritionDB")
                ?? throw new InvalidOperationException("ConnectionStrings:EzNutritionDB is missing.");
            var applicationConnectionString = builder.Configuration.GetConnectionString("ApplicationDb")
                ?? throw new InvalidOperationException("ConnectionStrings:ApplicationDb is missing.");
            var applyDatabaseMigrations = builder.Configuration.GetValue<bool?>(
                "DatabaseStartup:ApplyMigrationsOnStartup") ?? false;

            // Add services to the container.
            builder.Services.AddDbContext<EzNutritionDbContext>(options => options.UseSqlServer(nutritionConnectionString));
            builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(applicationConnectionString));
            builder.AuthorizeConfiguration();
            builder.Services.AddControllersWithViews();
            builder.Services.AddRazorPages();
            builder.Services.AddScoped<JwtService>();
            builder.Services.AddScoped<DietaryReferenceIntakeRepository>();
            builder.Services.AddScoped<AuthManagerRepository>();
            builder.Services.AddTransient<IEmailSender<IdentityUser>, SmtpEmailSender>();
            builder.Services.AddScoped<FoodNutritionValueRepository>();
            builder.Services.AddSingleton<CertificateFileStore>();
            builder.Services.AddHttpClient<IGenerativeAiProvider, TencentAgencyDeepSeekV4Pro>(client =>
            {
                // Streaming requests are bounded by the request cancellation token rather than HttpClient's default timeout.
                client.Timeout = Timeout.InfiniteTimeSpan;
            });

            builder.Services.AddOptions<EmailSettings>()
                .Bind(builder.Configuration.GetSection(nameof(EmailSettings)))
                .ValidateDataAnnotations()
                .ValidateOnStart();
            builder.Services.AddOptions<JwtSettings>()
                .Bind(builder.Configuration.GetSection(nameof(JwtSettings)))
                .ValidateDataAnnotations()
                .ValidateOnStart();
            builder.Services.AddOptions<TencentAgencyConfig>()
                .Bind(builder.Configuration.GetSection(nameof(TencentAgencyConfig)))
                .Validate(config => !string.IsNullOrWhiteSpace(config.SecretKey), "TencentAgencyConfig:SecretKey is missing.")
                .ValidateOnStart();


            var app = builder.Build();

            await using (var scope = app.Services.CreateAsyncScope())
            {
                var nutrDb = scope.ServiceProvider.GetRequiredService<EzNutritionDbContext>();
                var appDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                if (applyDatabaseMigrations)
                {
                    await nutrDb.Database.MigrateAsync();
                    await appDb.Database.MigrateAsync();
                }
                else
                {
                    await EnsureNoPendingMigrationsAsync(nutrDb, nameof(EzNutritionDbContext));
                    await EnsureNoPendingMigrationsAsync(appDb, nameof(ApplicationDbContext));
                    app.Logger.LogInformation("Database migrations are disabled for this startup.");
                }

                if (args.Any(x => string.Equals(x, "AuthInitialize", StringComparison.OrdinalIgnoreCase)))
                {
                    var auth = scope.ServiceProvider.GetRequiredService<AuthManagerRepository>();
                    await auth.Initialize();
                    return;
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

            await app.RunAsync();
        }

        private static async Task EnsureNoPendingMigrationsAsync(DbContext dbContext, string contextName)
        {
            var pendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync()).ToArray();
            if (pendingMigrations.Length > 0)
            {
                throw new InvalidOperationException(
                    $"{contextName} has pending migrations while startup migration is disabled: " +
                    string.Join(", ", pendingMigrations));
            }
        }
    }
}
