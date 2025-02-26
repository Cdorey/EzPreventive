using System.Text.Json;
using TencentCloud.Lkeap.V20240522;
using TencentCloud.Lkeap.V20240522.Models;
using EzNutrition.Shared.Data.DTO.PromptDto;
using Microsoft.Extensions.Options;

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

        public async IAsyncEnumerable<AiResultDto> Generate(PromptDto prompt)
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
                if (item.Data == "[DONE]")
                    yield break;
                else
                {
                    var chunk = JsonSerializer.Deserialize<AiResponseChunk>(item.Data);
                    if (chunk is not null)
                    {
                        foreach (var choice in chunk.Choices)
                        {
                            yield return new AiResultDto(choice.Delta?.Content ?? choice.Delta?.ReasoningContent ?? string.Empty, choice.Delta?.Content is null);
                        }
                    }
                }
            }
        }
    }
}