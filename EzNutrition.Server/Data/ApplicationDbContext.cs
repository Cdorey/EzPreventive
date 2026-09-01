using EzNutrition.Server.Data.Entities;
using EzNutrition.Shared.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EzNutrition.Server.Data
{
    public class ApplicationDbContext(DbContextOptions options) : IdentityDbContext(options)
    {
        /// <summary>
        /// 按 UTC 约定写入认证时间，并在从无时区信息的 datetime2 读取后恢复 UTC Kind。
        /// </summary>
        private static readonly ValueConverter<DateTime, DateTime> UtcDateTimeConverter = new(
            value => value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc),
            value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

        public DbSet<Notice> Notices { get; set; }

        public DbSet<ProfessionalCertificationRequest> ProfessionalCertificationRequests { get; set; }

        public DbSet<PrescriptionGenerateRequest> PrescriptionGenerateRequests { get; set; }

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            var certificationRequest = builder.Entity<ProfessionalCertificationRequest>();
            certificationRequest
                .Property(request => request.RequestTime)
                .HasConversion(UtcDateTimeConverter);
            certificationRequest
                .Property(request => request.ProcessedTime)
                .HasConversion(UtcDateTimeConverter);
        }
    }
}
