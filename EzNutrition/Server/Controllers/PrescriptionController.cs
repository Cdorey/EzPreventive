using EzNutrition.AiAgency;
using EzNutrition.Server.Data;
using EzNutrition.Server.Data.Entities;
using EzNutrition.Server.Data.Repositories;
using EzNutrition.Shared.Data.DTO.PromptDto;
using EzNutrition.Shared.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace EzNutrition.Server.Controllers
{
    [ApiController]
    [Route("[controller]/[Action]")]
    [Authorize(Policy = PolicyList.Prescription)]
    public class PrescriptionController(IGenerativeAiProvider generator, ApplicationDbContext applicationDb) : ControllerBase
    {
        [HttpGet]
        public IActionResult Environment()
        {
            return Ok(new EnvironmentDto(generator.ProviderName, generator.PlatformDetails, generator.AdditionalInfo));
        }

        public async Task<IActionResult> Generate([FromBody] PromptDto prompt)
        {
            var generateRequest = new PrescriptionGenerateRequest
            {
                UserId = User.Claims.First(c => c.Type == ClaimTypes.Upn).Value,
                Prompt = JsonSerializer.Serialize(prompt),
                RequestTime = DateTime.Now,
            };
            var reasoningSB = new StringBuilder();
            var contentSB = new StringBuilder();
            Response.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-cache";


            try
            {
                await foreach (var result in generator.Generate(prompt))
                {
                    await Response.WriteAsync($"data: {JsonSerializer.Serialize(result)}\n\n");
                    if (result.IsReasoningContent)
                    {
                        reasoningSB.Append(result.Content);
                    }
                    else
                    {
                        contentSB.Append(result.Content);
                    }
                    await Response.Body.FlushAsync();
                }
            }
            catch (Exception)
            {
                // 发送 SSE 错误事件或记录日志
                await Response.WriteAsync($"data: {{\"error\": \"AI 生成器调用异常\"}}\n\n");
                // 可以再发 [DONE] 或直接中断
            }
            finally
            {
                generateRequest.ProcessedTime = DateTime.Now;
                generateRequest.ReasoningContent = reasoningSB.ToString();
                generateRequest.Content = contentSB.ToString();
                applicationDb.Add(generateRequest);
                await applicationDb.SaveChangesAsync();
                await Response.WriteAsync("data: [DONE]\n\n");
                await Response.Body.FlushAsync();
            }
            return new EmptyResult();
        }
    }
}