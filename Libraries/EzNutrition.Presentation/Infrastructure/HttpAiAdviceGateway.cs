using EzNutrition.Application.Ports;
using EzNutrition.Shared.Data.DTO.PromptDto;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace EzNutrition.Presentation.Infrastructure;

/// <summary>
/// 使用 EzNutrition HTTP 与 SSE 协议实现交互式客户端共享的 AI 建议端口。
/// </summary>
public sealed class HttpAiAdviceGateway(IHttpClientFactory httpClientFactory) : IAiAdviceGateway
{
    private const string EnvironmentEndpoint = "Prescription/Environment";
    private const string GenerateEndpoint = "Prescription/Generate";
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public async Task<EnvironmentDto?> GetEnvironmentAsync(
        CancellationToken cancellationToken = default)
    {
        var httpClient = httpClientFactory.CreateClient("Authorize");
        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync(EnvironmentEndpoint, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsTransportFailure(ex))
        {
            throw new AiAdviceAccessException("Unable to load AI environment information.", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new AiAdviceAccessException(
                    $"The AI environment endpoint returned HTTP {(int)response.StatusCode}.",
                    MapFailureKind(response.StatusCode));
            }

            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                return await JsonSerializer.DeserializeAsync<EnvironmentDto>(
                    stream,
                    WebJsonOptions,
                    cancellationToken)
                    ?? throw new AiAdviceProtocolException("The AI environment response was empty.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (AiAdviceProtocolException)
            {
                throw;
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidDataException)
            {
                throw new AiAdviceProtocolException("The AI environment response was invalid.", ex);
            }
            catch (Exception ex) when (IsTransportFailure(ex))
            {
                throw new AiAdviceAccessException("Unable to read AI environment information.", ex);
            }
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<AiAdviceGatewayUpdate> GenerateAsync(
        AiAdviceRequestDto requestData,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestData);

        using var request = new HttpRequestMessage(HttpMethod.Post, GenerateEndpoint);
        string json;
        try
        {
            json = JsonSerializer.Serialize(requestData, AiAdviceJson.Compact);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new AiAdviceProtocolException("The AI advice request could not be serialized.", ex);
        }

        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        var httpClient = httpClientFactory.CreateClient("AiAuthorize");
        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsTransportFailure(ex))
        {
            throw new AiAdviceAccessException("Unable to submit the AI advice request.", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new AiAdviceAccessException(
                    $"The AI generation endpoint returned HTTP {(int)response.StatusCode}.",
                    MapFailureKind(response.StatusCode));
            }

            Stream stream;
            try
            {
                stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (IsTransportFailure(ex))
            {
                throw new AiAdviceAccessException("Unable to open the AI response stream.", ex);
            }

            yield return new AiAdviceGatewayUpdate(AiAdviceGatewayUpdateKind.Accepted);

            await using (stream)
            using (var reader = new StreamReader(stream))
            {
                var completed = false;
                while (true)
                {
                    string? line;
                    try
                    {
                        line = await reader.ReadLineAsync(cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex) when (IsTransportFailure(ex))
                    {
                        throw new AiAdviceAccessException("The AI response stream was interrupted.", ex);
                    }

                    if (line is null)
                    {
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(line)
                        || !line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var payload = line["data:".Length..].Trim();
                    if (payload == "[DONE]")
                    {
                        completed = true;
                        break;
                    }

                    AiResultDto chunk;
                    try
                    {
                        chunk = JsonSerializer.Deserialize<AiResultDto>(payload)
                            ?? throw new JsonException("The AI stream contained an empty data chunk.");
                    }
                    catch (JsonException ex)
                    {
                        throw new AiAdviceProtocolException("The AI stream contained invalid JSON.", ex);
                    }

                    if (chunk.IsError)
                    {
                        throw new AiAdviceProviderException(chunk.Content);
                    }

                    if (string.IsNullOrEmpty(chunk.Content))
                    {
                        continue;
                    }

                    yield return new AiAdviceGatewayUpdate(
                        chunk.IsReasoningContent
                            ? AiAdviceGatewayUpdateKind.Reasoning
                            : AiAdviceGatewayUpdateKind.Recommendation,
                        chunk.Content);
                }

                if (!completed)
                {
                    throw new AiAdviceProtocolException(
                        "The AI stream ended without a completion marker.");
                }
            }
        }
    }

    private static bool IsTransportFailure(Exception exception) => exception is
        HttpRequestException or
        IOException or
        InvalidOperationException or
        ObjectDisposedException or
        OperationCanceledException;

    private static AiAdviceAccessFailureKind MapFailureKind(System.Net.HttpStatusCode statusCode)
    {
        if (statusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
        {
            return AiAdviceAccessFailureKind.AccessDenied;
        }

        return statusCode is System.Net.HttpStatusCode.RequestTimeout
            or System.Net.HttpStatusCode.TooManyRequests
            || (int)statusCode >= 500
                ? AiAdviceAccessFailureKind.Unavailable
                : AiAdviceAccessFailureKind.Rejected;
    }
}
