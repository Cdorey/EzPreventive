using EzNutrition.Server.Controllers;
using EzNutrition.Server.Data.Entities;
using EzNutrition.Server.Extension;
using EzNutrition.Server.Services;
using EzNutrition.Shared.Data.DTO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EzNutrition.Server.Tests.Repositories;

public sealed partial class AuthManagerRepositoryLoginTests
{
    [Theory]
    [InlineData(RequestStatus.Pending)]
    [InlineData(RequestStatus.Approved)]
    [InlineData(RequestStatus.Rejected)]
    public async Task Certification_review_preserves_submission_and_roles_and_cleans_only_completed_evidence(
        RequestStatus status)
    {
        var now = new DateTimeOffset(2026, 9, 3, 1, 2, 3, TimeSpan.Zero);
        await using var host = LoginTestHost.Create(timeProvider: new FixedTimeProvider(now));
        var user = await host.CreateUserAsync("review-user", "review@example.test", emailConfirmed: true);
        var role = new IdentityRole("ExistingRole");
        host.DbContext.Roles.Add(role);
        host.DbContext.UserRoles.Add(new IdentityUserRole<string> { UserId = user.Id, RoleId = role.Id });
        var request = CreateReviewRequest(user.Id, now.AddDays(-10).UtcDateTime);
        host.DbContext.ProfessionalCertificationRequests.Add(request);
        await host.DbContext.SaveChangesAsync();
        var store = host.Services.GetRequiredService<CertificateFileStore>();
        var originalTicket = request.CertificateTicket!.Value;
        var suppliedTicket = Guid.NewGuid();
        await SaveReviewEvidenceAsync(store, originalTicket);
        await SaveReviewEvidenceAsync(store, suppliedTicket);
        var service = host.Services.GetRequiredService<CertificationReviewService>();

        var result = await service.UpdateAsync(request.Id, status, "审核意见", "备注", suppliedTicket);

        Assert.Equal(CertificationReviewStatus.Updated, result.Status);
        Assert.False(result.CertificateFileCleanupFailed);
        host.DbContext.ChangeTracker.Clear();
        var persisted = await host.DbContext.ProfessionalCertificationRequests.SingleAsync();
        Assert.Equal(status, persisted.Status);
        Assert.Equal(now.UtcDateTime, persisted.ProcessedTime);
        Assert.Equal(DateTimeKind.Utc, persisted.ProcessedTime?.Kind);
        Assert.Equal(request.RequestTime, persisted.RequestTime);
        Assert.Equal(request.UserId, persisted.UserId);
        Assert.Equal(request.IdentityType, persisted.IdentityType);
        Assert.Equal(request.InstitutionName, persisted.InstitutionName);
        Assert.Equal("审核意见", persisted.ProcessDetails);
        Assert.Equal("备注", persisted.Remarks);
        Assert.Equal(status == RequestStatus.Pending ? suppliedTicket : (Guid?)null, persisted.CertificateTicket);
        Assert.Equal(role.Id, (await host.DbContext.UserRoles.SingleAsync()).RoleId);
        Assert.Empty(await host.DbContext.UserClaims.ToListAsync());
        AssertReviewEvidence(store, originalTicket, expected: status == RequestStatus.Pending);
        AssertReviewEvidence(store, suppliedTicket, expected: true);
    }

    [Theory]
    [InlineData(false, 400)]
    [InlineData(true, 404)]
    public async Task Certification_review_http_errors_do_not_change_data_or_files(bool missing, int expectedStatus)
    {
        await using var host = LoginTestHost.Create();
        var request = CreateReviewRequest("review-user", DateTime.UtcNow.AddDays(-10));
        host.DbContext.ProfessionalCertificationRequests.Add(request);
        await host.DbContext.SaveChangesAsync();
        var store = host.Services.GetRequiredService<CertificateFileStore>();
        await SaveReviewEvidenceAsync(store, request.CertificateTicket!.Value);
        var dto = request.ToDto();
        dto.Id = missing ? Guid.NewGuid() : request.Id;
        dto.Status = missing ? RequestStatus.Rejected : (RequestStatus)999;
        var controller = ActivatorUtilities.CreateInstance<AdminController>(host.Services);

        var result = await controller.UpdateRequest(dto, CancellationToken.None);

        var response = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(missing ? "请求不存在" : "认证请求状态无效。", response.Value);
        host.DbContext.ChangeTracker.Clear();
        var persisted = await host.DbContext.ProfessionalCertificationRequests.SingleAsync();
        Assert.Equal(RequestStatus.Pending, persisted.Status);
        Assert.Null(persisted.ProcessedTime);
        Assert.Equal(request.CertificateTicket, persisted.CertificateTicket);
        AssertReviewEvidence(store, request.CertificateTicket.Value, expected: true);
    }

    [Fact]
    public async Task Certification_review_keeps_evidence_when_database_save_fails()
    {
        await using var host = LoginTestHost.Create();
        var request = CreateReviewRequest("review-user", DateTime.UtcNow.AddDays(-10));
        host.DbContext.ProfessionalCertificationRequests.Add(request);
        await host.DbContext.SaveChangesAsync();
        var ticket = request.CertificateTicket!.Value;
        var store = host.Services.GetRequiredService<CertificateFileStore>();
        await SaveReviewEvidenceAsync(store, ticket);
        await host.DbContext.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER reject_review BEFORE UPDATE ON ProfessionalCertificationRequests
            BEGIN SELECT RAISE(ABORT, 'simulated review save failure'); END;
            """);
        var service = host.Services.GetRequiredService<CertificationReviewService>();

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            service.UpdateAsync(request.Id, RequestStatus.Rejected, "超时", null, null));

        host.DbContext.ChangeTracker.Clear();
        var persisted = await host.DbContext.ProfessionalCertificationRequests.SingleAsync();
        Assert.Equal(RequestStatus.Pending, persisted.Status);
        Assert.Null(persisted.ProcessedTime);
        Assert.Equal(ticket, persisted.CertificateTicket);
        AssertReviewEvidence(store, ticket, expected: true);
    }

    [ReviewWindowsFact]
    public async Task Certification_review_reports_file_failure_without_reverting_saved_review()
    {
        await using var host = LoginTestHost.Create();
        var request = CreateReviewRequest("review-user", DateTime.UtcNow.AddDays(-10));
        host.DbContext.ProfessionalCertificationRequests.Add(request);
        await host.DbContext.SaveChangesAsync();
        var ticket = request.CertificateTicket!.Value;
        var store = host.Services.GetRequiredService<CertificateFileStore>();
        await SaveReviewEvidenceAsync(store, ticket);
        var evidence = store.OpenRead(ticket);
        Assert.NotNull(evidence);
        using var lockedFile = evidence.Content;
        var service = host.Services.GetRequiredService<CertificationReviewService>();

        var result = await service.UpdateAsync(request.Id, RequestStatus.Rejected, null, null, null);

        Assert.Equal(CertificationReviewStatus.Updated, result.Status);
        Assert.True(result.CertificateFileCleanupFailed);
        host.DbContext.ChangeTracker.Clear();
        var persisted = await host.DbContext.ProfessionalCertificationRequests.SingleAsync();
        Assert.Equal(RequestStatus.Rejected, persisted.Status);
        Assert.NotNull(persisted.ProcessedTime);
        Assert.Null(persisted.CertificateTicket);
        AssertReviewEvidence(store, ticket, expected: true);
    }

    /// <summary>创建带有原始提交时间和图片引用的待审核申请。</summary>
    private static ProfessionalCertificationRequest CreateReviewRequest(string userId, DateTime requestTime) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        RequestTime = requestTime,
        IdentityType = "Physician",
        InstitutionName = "Test Institution",
        Status = RequestStatus.Pending,
        CertificateTicket = Guid.NewGuid()
    };

    /// <summary>通过真实文件存储写入隔离测试目录，验证审核后的文件生命周期。</summary>
    private static async Task SaveReviewEvidenceAsync(CertificateFileStore store, Guid ticket)
    {
        using var content = new MemoryStream([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var file = new FormFile(content, 0, content.Length, "certificate", "certificate.png");
        await store.SaveAsync(ticket, file, CancellationToken.None);
    }

    /// <summary>检查图片是否仍可读取，并及时释放文件句柄。</summary>
    private static void AssertReviewEvidence(CertificateFileStore store, Guid ticket, bool expected)
    {
        var evidence = store.OpenRead(ticket);
        using var content = evidence?.Content;
        Assert.Equal(expected, evidence is not null);
    }

    /// <summary>文件占用测试依赖 Windows 的共享删除限制。</summary>
    private sealed class ReviewWindowsFactAttribute : FactAttribute
    {
        public ReviewWindowsFactAttribute()
        {
            if (!OperatingSystem.IsWindows())
            {
                Skip = "需要 Windows 的 FileShare 删除限制。";
            }
        }
    }
}
