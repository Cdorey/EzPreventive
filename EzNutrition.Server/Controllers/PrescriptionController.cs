using EzNutrition.AiAgency;
using EzNutrition.Server.Data;
using EzNutrition.Server.Data.Entities;
using EzNutrition.Server.Data.Repositories;
using EzNutrition.Server.Services;
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
    public class PrescriptionController(
        IGenerativeAiProvider generator,
        ApplicationDbContext applicationDb,
        AiAdvicePromptComposer promptComposer,
        ILogger<PrescriptionController> logger,
        TimeProvider timeProvider) : ControllerBase
    {
        [HttpGet]
        public IActionResult Environment()
        {
            return Ok(new EnvironmentDto(generator.ProviderName, generator.PlatformDetails, generator.AdditionalInfo));
        }

        [HttpPost]
        [Consumes("application/json")]
        [RequestSizeLimit(1024 * 1024)]
        public async Task<IActionResult> Generate([FromBody] AiAdviceRequestDto request)
        {
            var userId = User.FindFirstValue(ClaimTypes.Upn);
            if (string.IsNullOrWhiteSpace(userId))
            {
                logger.LogWarning("A prescription request reached the controller without a user id claim.");
                return Unauthorized();
            }

            var cancellationToken = HttpContext.RequestAborted;
            var chatPrompt = promptComposer.Compose(request);

            var generateRequest = new PrescriptionGenerateRequest
            {
                UserId = userId,
                Prompt = chatPrompt.UserMessage,
                RequestTime = timeProvider.GetUtcNow().UtcDateTime,
            };

            // Fail closed: the AI request is not sent unless its audit record has been persisted first.
            applicationDb.Add(generateRequest);
            await applicationDb.SaveChangesAsync(cancellationToken);

            var reasoningSB = new StringBuilder();
            var contentSB = new StringBuilder();
            Response.ContentType = "text/event-stream; charset=utf-8";
            Response.Headers.CacheControl = "no-cache, no-store";
            Response.Headers["X-Accel-Buffering"] = "no";

            try
            {
                // Commit the SSE response immediately so the browser can distinguish a request
                // accepted by EzNutrition from time spent waiting for the upstream model.
                await Response.StartAsync(cancellationToken);
                await Response.WriteAsync(": connected\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);

                await foreach (var result in generator.Generate(chatPrompt, cancellationToken))
                {
                    if (result.IsReasoningContent)
                    {
                        reasoningSB.Append(result.Content);
                    }
                    else
                    {
                        contentSB.Append(result.Content);
                    }

                    await WriteEventAsync(result, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation(
                    "Prescription generation {RequestId} was cancelled by user {UserId}.",
                    generateRequest.Id,
                    userId);
            }
            catch (IOException ex)
            {
                logger.LogInformation(
                    ex,
                    "Prescription response {RequestId} was closed by user {UserId}.",
                    generateRequest.Id,
                    userId);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Prescription generation {RequestId} failed for user {UserId}.",
                    generateRequest.Id,
                    userId);

                var errorResult = new AiResultDto("AI 生成器调用异常，请稍后重试。", false, true);
                contentSB.Append(errorResult.Content);
                if (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        await WriteEventAsync(errorResult, cancellationToken);
                    }
                    catch (Exception writeException) when (writeException is IOException or OperationCanceledException)
                    {
                        logger.LogDebug(
                            writeException,
                            "The prescription SSE response closed before an error event could be sent.");
                    }
                }
            }
            finally
            {
                generateRequest.ProcessedTime = timeProvider.GetUtcNow().UtcDateTime;
                generateRequest.ReasoningContent = reasoningSB.ToString();
                generateRequest.Content = contentSB.ToString();

                try
                {
                    // Preserve the audit trail even when the browser has disconnected.
                    await applicationDb.SaveChangesAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.LogCritical(
                        ex,
                        "Failed to finalize prescription audit record {RequestId} for user {UserId}.",
                        generateRequest.Id,
                        userId);
                }

                if (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        await Response.WriteAsync("data: [DONE]\n\n", cancellationToken);
                        await Response.Body.FlushAsync(cancellationToken);
                    }
                    catch (Exception ex) when (ex is IOException or OperationCanceledException)
                    {
                        logger.LogDebug(ex, "The prescription SSE response closed before the final marker was sent.");
                    }
                }
            }

            return new EmptyResult();
        }

        private async Task WriteEventAsync(AiResultDto result, CancellationToken cancellationToken)
        {
            await Response.WriteAsync($"data: {JsonSerializer.Serialize(result)}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }
}
