using EzNutrition.Server.Data;
using EzNutrition.Server.Data.Entities;
using EzNutrition.Server.Services;
using EzNutrition.Server.Services.Maintenance;
using EzNutrition.Shared.Data.DTO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace EzNutrition.Server.Tests.Services;

public sealed class OrphanCleanupServiceTests
{
    [Fact]
    public async Task Certificate_cleanup_removes_only_old_recognized_orphans()
    {
        await using var host = TestHost.Create();
        var cutoff = DateTimeOffset.UtcNow.AddHours(-1);
        var oldTimestamp = cutoff.AddHours(-1);
        var referencedTicket = Guid.NewGuid();
        var trackedTicket = Guid.NewGuid();
        var orphanedTicket = Guid.NewGuid();
        var freshOrphanedTicket = Guid.NewGuid();

        await host.SaveCertificateAsync(referencedTicket);
        await host.SaveCertificateAsync(trackedTicket);
        await host.SaveCertificateAsync(orphanedTicket);
        await host.SaveCertificateAsync(freshOrphanedTicket);
        host.SetCertificateTimestamp(referencedTicket, oldTimestamp);
        host.SetCertificateTimestamp(trackedTicket, oldTimestamp);
        host.SetCertificateTimestamp(orphanedTicket, oldTimestamp);

        host.DbContext.ProfessionalCertificationRequests.Add(
            CreateCertificationRequest("persisted-user", referencedTicket));
        await host.DbContext.SaveChangesAsync();
        host.DbContext.ProfessionalCertificationRequests.Add(
            CreateCertificationRequest("unsaved-user", trackedTicket));

        var temporaryPath = await host.CreateTemporaryFileAsync(Guid.NewGuid(), oldTimestamp);
        var unknownPath = await host.CreateUnknownFileAsync(oldTimestamp);

        var result = await host.Service.DeleteOrphanedCertificateFilesAsync(cutoff);

        Assert.Equal(5, result.RecognizedFiles);
        Assert.Equal(2, result.CleanupCandidates);
        Assert.Equal(2, result.DeletedFiles);
        Assert.Equal(0, result.FailedFiles);
        AssertCertificateExists(host.CertificateFileStore, referencedTicket);
        AssertCertificateExists(host.CertificateFileStore, trackedTicket);
        Assert.Null(host.CertificateFileStore.OpenRead(orphanedTicket));
        AssertCertificateExists(host.CertificateFileStore, freshOrphanedTicket);
        Assert.False(File.Exists(temporaryPath));
        Assert.True(File.Exists(unknownPath));

        var secondResult = await host.Service.DeleteOrphanedCertificateFilesAsync(cutoff);

        Assert.Equal(3, secondResult.RecognizedFiles);
        Assert.Equal(0, secondResult.CleanupCandidates);
        Assert.Equal(0, secondResult.DeletedFiles);
        Assert.Equal(0, secondResult.FailedFiles);
    }

    [Fact]
    public async Task Ai_audit_cleanup_removes_only_records_without_identity_users()
    {
        await using var host = TestHost.Create();
        var liveUser = new IdentityUser
        {
            Id = "live-user",
            UserName = "live-user",
            NormalizedUserName = "LIVE-USER"
        };
        host.DbContext.Users.Add(liveUser);
        host.DbContext.PrescriptionGenerateRequests.AddRange(
            CreateAiAudit(liveUser.Id),
            CreateAiAudit("deleted-user"));
        await host.DbContext.SaveChangesAsync();
        var unsavedOrphan = CreateAiAudit("never-saved-user");
        host.DbContext.PrescriptionGenerateRequests.Add(unsavedOrphan);

        var deletedRecords = await host.Service.DeleteOrphanedAiAuditRecordsAsync();
        await host.DbContext.SaveChangesAsync();

        Assert.Equal(1, deletedRecords);
        Assert.Equal(EntityState.Detached, host.DbContext.Entry(unsavedOrphan).State);
        var remainingRecords = await host.DbContext.PrescriptionGenerateRequests
            .AsNoTracking()
            .ToArrayAsync();
        var remainingRecord = Assert.Single(remainingRecords);
        Assert.Equal(liveUser.Id, remainingRecord.UserId);
        Assert.Equal(0, await host.Service.DeleteOrphanedAiAuditRecordsAsync());
    }

    private static void AssertCertificateExists(CertificateFileStore store, Guid ticket)
    {
        var certificate = store.OpenRead(ticket);
        Assert.NotNull(certificate);
        certificate.Content.Dispose();
    }

    private static ProfessionalCertificationRequest CreateCertificationRequest(
        string userId,
        Guid certificateTicket) => new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RequestTime = DateTime.UtcNow,
            IdentityType = "Physician",
            InstitutionName = "Test Institution",
            Status = RequestStatus.Pending,
            CertificateTicket = certificateTicket
        };

    private static PrescriptionGenerateRequest CreateAiAudit(string userId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Prompt = "audit prompt",
        Content = "audit response",
        RequestTime = DateTime.UtcNow,
        ProcessedTime = DateTime.UtcNow
    };

    private sealed class TestHost : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly string contentRootPath;

        private TestHost(
            SqliteConnection connection,
            string contentRootPath,
            ApplicationDbContext dbContext,
            CertificateFileStore certificateFileStore,
            OrphanCleanupService service)
        {
            this.connection = connection;
            this.contentRootPath = contentRootPath;
            DbContext = dbContext;
            CertificateFileStore = certificateFileStore;
            Service = service;
        }

        internal ApplicationDbContext DbContext { get; }

        internal CertificateFileStore CertificateFileStore { get; }

        internal OrphanCleanupService Service { get; }

        private string UploadRootPath => Path.Combine(contentRootPath, "TempUploads");

        internal static TestHost Create()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;
            var dbContext = new ApplicationDbContext(options);
            dbContext.Database.EnsureCreated();
            var contentRootPath = Path.Combine(
                Path.GetTempPath(),
                "EzNutrition.Server.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(contentRootPath);
            var certificateFileStore = new CertificateFileStore(
                new TestWebHostEnvironment(contentRootPath),
                NullLogger<CertificateFileStore>.Instance);
            var service = new OrphanCleanupService(
                dbContext,
                certificateFileStore,
                NullLogger<OrphanCleanupService>.Instance);
            return new TestHost(
                connection,
                contentRootPath,
                dbContext,
                certificateFileStore,
                service);
        }

        internal async Task SaveCertificateAsync(Guid ticket)
        {
            byte[] pngBytes =
            [
                0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
                0x00, 0x00, 0x00, 0x00
            ];
            await using var content = new MemoryStream(pngBytes);
            var file = new FormFile(content, 0, content.Length, "certificate", "certificate.png");
            await CertificateFileStore.SaveAsync(ticket, file, CancellationToken.None);
        }

        internal void SetCertificateTimestamp(Guid ticket, DateTimeOffset timestamp) =>
            File.SetLastWriteTimeUtc(
                Path.Combine(UploadRootPath, $"{ticket:D}.png"),
                timestamp.UtcDateTime);

        internal async Task<string> CreateTemporaryFileAsync(
            Guid ticket,
            DateTimeOffset timestamp)
        {
            Directory.CreateDirectory(UploadRootPath);
            var path = Path.Combine(UploadRootPath, $".{ticket:D}.{Guid.NewGuid():N}.tmp");
            await File.WriteAllBytesAsync(path, [0x01, 0x02, 0x03]);
            File.SetLastWriteTimeUtc(path, timestamp.UtcDateTime);
            return path;
        }

        internal async Task<string> CreateUnknownFileAsync(DateTimeOffset timestamp)
        {
            Directory.CreateDirectory(UploadRootPath);
            var path = Path.Combine(UploadRootPath, "manual-note.tmp");
            await File.WriteAllTextAsync(path, "do not delete");
            File.SetLastWriteTimeUtc(path, timestamp.UtcDateTime);
            return path;
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await connection.DisposeAsync();
            if (Directory.Exists(contentRootPath))
            {
                Directory.Delete(contentRootPath, recursive: true);
            }
        }
    }

    private sealed class TestWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = typeof(OrphanCleanupServiceTests).Assembly.FullName!;

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string WebRootPath { get; set; } = contentRootPath;

        public string EnvironmentName { get; set; } = Environments.Development;

        public string ContentRootPath { get; set; } = contentRootPath;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
