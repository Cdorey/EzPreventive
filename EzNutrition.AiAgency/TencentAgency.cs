using EzNutrition.Shared.Data.DTO.PromptDto;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using TencentCloud.Lkeap.V20240522;
using TencentCloud.Lkeap.V20240522.Models;

namespace EzNutrition.AiAgency
{
    public class TencentAgency(IOptions<TencentAgencyConfig> options) : IGenerativeAiProvider
    {
        public string ProviderName => "Tencent Cloud LKEAP";

        public string PlatformDetails => "ap-shanghai region, deepseek-r1 model";

        public string AdditionalInfo => "Streaming mode, max tokens=51200, temperature=0.8";

        private readonly LkeapClient client = new(new()
        {
            SecretId = options.Value.SecretId,
            SecretKey = options.Value.SecretKey
        }, "ap-shanghai");

        private class AiResponseChunk
        {
            public Choice[] Choices { get; set; } = [];
        }

        private class Choice
        {
            public string? FinishReason { get; set; }

            public Delta? Delta { get; set; }

            public long? Index { get; set; }
        }

        private class Delta
        {
            public string? ReasoningContent { get; set; }
            public string? Content { get; set; }
            public string? Role { get; set; }
        }

        public async IAsyncEnumerable<AiResultDto> Generate(
            PromptDto prompt,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var rep = new ChatCompletionsRequest
            {
                Model = "deepseek-r1",
                Messages = [new Message {
                    Role="user",
                    Content=JsonSerializer.Serialize(prompt)
                }],
                Stream = true,
                Temperature = 0.8f,
                MaxTokens = 51200
            };
            var x = await client.ChatCompletions(rep);
            foreach (var item in x)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (item.Data == "[DONE]")
                    yield break;
                else
                {
                    var chunk = JsonSerializer.Deserialize<AiResponseChunk>(item.Data);
                    if (chunk is not null)
                    {
                        foreach (var choice in chunk.Choices)
                        {
                            var isReasoningContent = string.IsNullOrEmpty(choice.Delta?.Content);
                            yield return new AiResultDto((isReasoningContent ? choice.Delta?.ReasoningContent : choice.Delta?.Content) ?? string.Empty, isReasoningContent);
                        }
                    }
                }
            }
        }
    }

    public class TencentAgencyDeepSeekV4Pro(
        IOptions<TencentAgencyConfig> options,
        HttpClient httpClient,
        ILogger<TencentAgencyDeepSeekV4Pro> logger) : IGenerativeAiProvider
    {
        public string ProviderName => "Tencent Cloud TokenHub";

        public string PlatformDetails => "Guangzhou region, deepseek-v4-pro model";

        public string AdditionalInfo => "Streaming mode, max tokens=384k";

        private readonly string apiKey = options.Value.SecretKey;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private class AiResponseChunk
        {
            [JsonPropertyName("choices")]
            public Choice[] Choices { get; set; } = [];
        }

        private class Choice
        {
            [JsonPropertyName("finish_reason")]
            public string? FinishReason { get; set; }

            [JsonPropertyName("delta")]
            public Delta? Delta { get; set; }

            [JsonPropertyName("index")]
            public long? Index { get; set; }
        }

        private class Delta
        {
            [JsonPropertyName("reasoning_content")]
            public string? ReasoningContent { get; set; }

            [JsonPropertyName("content")]
            public string? Content { get; set; }

            [JsonPropertyName("role")]
            public string? Role { get; set; }
        }

        public async IAsyncEnumerable<AiResultDto> Generate(
            PromptDto prompt,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://tokenhub.tencentmaas.com/v1/chat/completions");

            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            var requestBody = new
            {
                model = "deepseek-v4-pro",

                messages = new object[]
                {
            new
            {
                role = "system",
                content = JsonSerializer.Serialize(prompt.DialogConfiguration)
            },
            new
            {
                role = "user",
                content = JsonSerializer.Serialize(prompt)
            }
                },

                stream = true,

                temperature = 0.8,

                // 如果接口支持思考模式，保留这一段
                thinking = new
                {
                    type = "enabled"
                },

                //// 可选：low / medium / high
                //reasoning_effort = "high"
            };

            var json = JsonSerializer.Serialize(requestBody);

            request.Content = new StringContent(
                json,
                System.Text.Encoding.UTF8,
                "application/json");

            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                var logBody = error.Length <= 2048 ? error : error[..2048];
                logger.LogError(
                    "Tencent TokenHub returned HTTP {StatusCode}. Response: {ResponseBody}",
                    (int)response.StatusCode,
                    logBody);

                throw new HttpRequestException(
                    "The AI provider rejected the request.",
                    null,
                    response.StatusCode);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);

            while (true)
            {
                var line = await reader.ReadLineAsync(cancellationToken);

                if (line is null)
                {
                    throw new EndOfStreamException("Tencent TokenHub closed the stream without a completion marker.");
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var data = line["data:".Length..].Trim();

                if (data == "[DONE]")
                {
                    yield break;
                }

                AiResponseChunk? chunk;

                try
                {
                    chunk = JsonSerializer.Deserialize<AiResponseChunk>(data, JsonOptions);
                }
                catch (JsonException)
                {
                    logger.LogDebug("Tencent TokenHub returned a malformed SSE data chunk; the chunk was ignored.");
                    continue;
                }

                if (chunk?.Choices is null || chunk.Choices.Length == 0)
                {
                    continue;
                }

                foreach (var choice in chunk.Choices)
                {
                    var delta = choice.Delta;

                    if (delta is not null)
                    {
                        if (!string.IsNullOrEmpty(delta.ReasoningContent))
                        {
                            yield return new AiResultDto(delta.ReasoningContent, true);
                        }

                        if (!string.IsNullOrEmpty(delta.Content))
                        {
                            yield return new AiResultDto(delta.Content, false);
                        }
                    }

                    if (!string.IsNullOrEmpty(choice.FinishReason))
                    {
                        yield break;
                    }
                }
            }
        }
    }
}
