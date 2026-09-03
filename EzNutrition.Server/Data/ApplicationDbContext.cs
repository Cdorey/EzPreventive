using EzNutrition.Server.Data.Entities;
using EzNutrition.Shared.Data.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EzNutrition.Server.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : IdentityDbContext<ApplicationUser>(options)
    {
        /// <summary>
        /// 按 UTC 约定写入业务时间，并在从无时区信息的 datetime2 读取后恢复 UTC Kind。
        /// </summary>
        private static readonly ValueConverter<DateTime, DateTime> UtcDateTimeConverter = new(
            value => value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc),
            value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

        public DbSet<Notice> Notices { get; set; }

        public DbSet<ProfessionalCertificationRequest> ProfessionalCertificationRequests { get; set; }

        public DbSet<PrescriptionGenerateRequest> PrescriptionGenerateRequests { get; set; }

        /// <summary>获取登录会话集合。</summary>
        public DbSet<AuthenticationSession> AuthenticationSessions { get; set; }

        /// <summary>获取刷新令牌的消费记录集合。</summary>
        public DbSet<RefreshTokenRecord> RefreshTokens { get; set; }

        /// <summary>获取可在运行时修改的应用配置。</summary>
        public DbSet<ApplicationSetting> ApplicationSettings { get; set; }

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            var setting = builder.Entity<ApplicationSetting>();
            setting.HasKey(item => item.Key);
            setting.Property(item => item.Key).HasMaxLength(128).IsUnicode(false);
            setting.Property(item => item.ValueJson).IsRequired();
            setting.Property(item => item.Version).IsConcurrencyToken();
            setting.Property(item => item.UpdatedAtUtc).HasConversion(UtcDateTimeConverter);
            setting.Property(item => item.UpdatedByUserId).HasMaxLength(450);

            var session = builder.Entity<AuthenticationSession>();
            session.Property(item => item.UserId).HasMaxLength(450);
            session.Property(item => item.SecurityStampFingerprint).HasMaxLength(64);
            session.Property(item => item.Version).IsConcurrencyToken();
            session.Property(item => item.CreatedAtUtc).HasConversion(UtcDateTimeConverter);
            session.Property(item => item.RefreshExpiresAtUtc).HasConversion(UtcDateTimeConverter);
            session.Property(item => item.ExpiresAtUtc).HasConversion(UtcDateTimeConverter);
            session.Property(item => item.RevokedAtUtc).HasConversion(UtcDateTimeConverter);
            session.HasIndex(item => item.RefreshExpiresAtUtc);
            session.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            var refreshToken = builder.Entity<RefreshTokenRecord>();
            refreshToken.Property(item => item.TokenHash).HasMaxLength(64).IsUnicode(false);
            refreshToken.HasIndex(item => item.TokenHash).IsUnique();
            refreshToken.Property(item => item.CreatedAtUtc).HasConversion(UtcDateTimeConverter);
            refreshToken.Property(item => item.ConsumedAtUtc).HasConversion(UtcDateTimeConverter);
            refreshToken.HasOne(item => item.Session).WithMany().HasForeignKey(item => item.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            var certificationRequest = builder.Entity<ProfessionalCertificationRequest>();
            certificationRequest.Property(request => request.UserId).HasMaxLength(450);
            certificationRequest.HasIndex(request => request.UserId);
            certificationRequest
                .Property(request => request.RequestTime)
                .HasConversion(UtcDateTimeConverter);
            certificationRequest
                .Property(request => request.ProcessedTime)
                .HasConversion(UtcDateTimeConverter);

            builder.Entity<Notice>()
                .Property(notice => notice.CreateTime)
                .HasConversion(UtcDateTimeConverter);

            var applicationUser = builder.Entity<ApplicationUser>();
            applicationUser
                .Property(user => user.CreatedAtUtc)
                .HasConversion(UtcDateTimeConverter);
            applicationUser
                .Property(user => user.LastSuccessfulLoginAtUtc)
                .HasConversion(UtcDateTimeConverter);

            var prescriptionGenerateRequest = builder.Entity<PrescriptionGenerateRequest>();
            prescriptionGenerateRequest.Property(request => request.UserId).HasMaxLength(450);
            prescriptionGenerateRequest.HasIndex(request => request.UserId);
            prescriptionGenerateRequest
                .Property(request => request.RequestTime)
                .HasConversion(UtcDateTimeConverter);
            prescriptionGenerateRequest
                .Property(request => request.ProcessedTime)
                .HasConversion(UtcDateTimeConverter);
        }
    }
}
