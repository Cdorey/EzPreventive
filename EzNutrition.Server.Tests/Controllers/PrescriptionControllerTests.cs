using EzNutrition.AiAgency;
using EzNutrition.Server.Controllers;
using EzNutrition.Server.Data;
using EzNutrition.Server.Services;
using EzNutrition.Shared.Data.DTO.PromptDto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Runtime.CompilerServices;
using System.Security.Claims;

namespace EzNutrition.Server.Tests.Controllers;

public sealed class PrescriptionControllerTests
{
    [Fact]
    public async Task Generate_audits_the_exact_user_message_sent_to_the_provider()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var applicationDb = new ApplicationDbContext(options);
        var provider = new CapturingProvider();
        var controller = new PrescriptionController(
            provider,
            applicationDb,
            new AiAdvicePromptComposer(),
            NullLogger<PrescriptionController>.Instance);
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Upn, "doctor@example.test")],
                "test"))
        };
        httpContext.Response.Body = new MemoryStream();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.Generate(CreateRequest());

        Assert.IsType<EmptyResult>(result);
        var sentPrompt = Assert.IsType<AiChatPrompt>(provider.ReceivedPrompt);
        var auditRecord = Assert.Single(applicationDb.PrescriptionGenerateRequests);
        Assert.Equal(sentPrompt.UserMessage, auditRecord.Prompt);
        Assert.NotEqual(sentPrompt.SystemMessage, auditRecord.Prompt);
        Assert.Equal("doctor@example.test", auditRecord.UserId);
        Assert.Equal("可执行建议", auditRecord.Content);
    }

    private static AiAdviceRequestDto CreateRequest() => new()
    {
        PatientInfo = new PatientInfo
        {
            Gender = "女",
            Age = new PatientAge(35),
            BMI = 22m,
            PAL = 1.5m,
            Height = 165m,
            Weight = 60m,
            TotalBalanceEnergyViaCalculation = 2000,
            SpecialPhysiologicalPeriod = null
        },
        ClinicalInfo = new ClinicalInfo { Subjective = "测试病史" }
    };

    private sealed class CapturingProvider : IGenerativeAiProvider
    {
        public string ProviderName => "test";

        public string PlatformDetails => "test";

        public string AdditionalInfo => "test";

        public AiChatPrompt? ReceivedPrompt { get; private set; }

        public async IAsyncEnumerable<AiResultDto> Generate(
            AiChatPrompt prompt,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ReceivedPrompt = prompt;
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return new AiResultDto("可执行建议", false);
        }
    }
}
