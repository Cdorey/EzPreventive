using EzNutrition.Server.Controllers;
using EzNutrition.Server.Data;
using EzNutrition.Shared.Data.DTO;
using EzNutrition.Shared.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EzNutrition.Server.Tests.Controllers;

/// <summary>
/// 验证匿名系统信息接口的稳定版本与兼容行为。
/// </summary>
public sealed class SystemInfoControllerTests
{
    [Fact]
    public void Notice_kind_values_preserve_the_legacy_boolean_mapping()
    {
        Assert.Equal(0, (int)NoticeKind.PostLoginAnnouncement);
        Assert.Equal(1, (int)NoticeKind.PreLoginAnnouncement);
    }

    [Fact]
    public async Task Public_info_returns_case_number_and_current_server_version()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CaseNumber"] = "test-case-number"
            })
            .Build();
        await using var db = CreateContext();
        var controller = new SystemInfoController(configuration, db);

        var result = controller.PublicInfo();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var publicInfo = Assert.IsType<PublicSystemInfoDto>(ok.Value);
        Assert.Equal("test-case-number", publicInfo.CaseNumber);
        Assert.Equal("2.2.0.0", publicInfo.ServerVersion);
    }

    [Fact]
    public async Task Public_info_leaves_an_unconfigured_case_number_empty()
    {
        var configuration = new ConfigurationBuilder().Build();
        await using var db = CreateContext();
        var controller = new SystemInfoController(configuration, db);

        var result = controller.PublicInfo();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var publicInfo = Assert.IsType<PublicSystemInfoDto>(ok.Value);
        Assert.Null(publicInfo.CaseNumber);
        Assert.Equal("2.2.0.0", publicInfo.ServerVersion);
    }

    [Fact]
    public async Task Notice_endpoints_return_the_latest_content_of_each_kind()
    {
        var configuration = new ConfigurationBuilder().Build();
        await using var db = CreateContext();
        var baseline = new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc);
        db.Notices.AddRange(
            CreateNotice(NoticeKind.PostLoginAnnouncement, "old-post-login", baseline),
            CreateNotice(NoticeKind.PostLoginAnnouncement, "post-login", baseline.AddMinutes(1)),
            CreateNotice(NoticeKind.PreLoginAnnouncement, "pre-login", baseline.AddMinutes(2)),
            CreateNotice(NoticeKind.UserAgreement, "agreement", baseline.AddMinutes(3)),
            CreateNotice(NoticeKind.PrivacyPolicy, "privacy", baseline.AddMinutes(4)));
        await db.SaveChangesAsync();
        var controller = new SystemInfoController(configuration, db);

        AssertNotice(
            await controller.CoverLetter(CancellationToken.None),
            NoticeKind.PreLoginAnnouncement,
            "pre-login");
        AssertNotice(
            await controller.Notice(CancellationToken.None),
            NoticeKind.PostLoginAnnouncement,
            "post-login");
        AssertNotice(
            await controller.UserAgreement(CancellationToken.None),
            NoticeKind.UserAgreement,
            "agreement");
        AssertNotice(
            await controller.PrivacyPolicy(CancellationToken.None),
            NoticeKind.PrivacyPolicy,
            "privacy");
    }

    private static Notice CreateNotice(NoticeKind kind, string description, DateTime createTime) => new()
    {
        Kind = kind,
        Description = description,
        PublisherId = "test-publisher",
        CreateTime = createTime
    };

    private static void AssertNotice(IActionResult result, NoticeKind kind, string description)
    {
        var ok = Assert.IsType<OkObjectResult>(result);
        var notice = Assert.IsType<Notice>(ok.Value);
        Assert.Equal(kind, notice.Kind);
        Assert.Equal(description, notice.Description);
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);
}
