using EzNutrition.Server.Controllers;
using EzNutrition.Server.Data;
using EzNutrition.Shared.Data.DTO;
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
        Assert.Equal("2.1.0.0", publicInfo.ServerVersion);
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
        Assert.Equal("2.1.0.0", publicInfo.ServerVersion);
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);
}
