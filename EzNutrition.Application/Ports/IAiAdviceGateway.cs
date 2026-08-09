using EzNutrition.Shared.Data.DTO.PromptDto;

namespace EzNutrition.Application.Ports;

/// <summary>
/// Defines the host-provided boundary used to obtain AI nutrition advice.
/// </summary>
/// <remarks>
/// Implementations may use browser HTTP streaming, desktop HTTP, or a local provider.
/// The reusable application and UI layers do not depend on those host details.
/// </remarks>
public interface IAiAdviceGateway
{
    /// <summary>Gets information about the AI provider used by the current host.</summary>
    Task<EnvironmentDto?> GetEnvironmentAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates advice as semantic updates. Normal completion means that the host
    /// adapter observed the transport's explicit completion signal.
    /// </summary>
    IAsyncEnumerable<AiAdviceGatewayUpdate> GenerateAsync(
        PromptDto prompt,
        CancellationToken cancellationToken = default);
}

/// <summary>Identifies the meaning of one host-independent generation update.</summary>
public enum AiAdviceGatewayUpdateKind
{
    /// <summary>The host accepted the request and opened its response.</summary>
    Accepted = 0,

    /// <summary>A fragment of model reasoning was received.</summary>
    Reasoning = 1,

    /// <summary>A fragment of the recommendation was received.</summary>
    Recommendation = 2
}

/// <summary>Represents one host-independent AI generation update.</summary>
public sealed record AiAdviceGatewayUpdate(
    AiAdviceGatewayUpdateKind Kind,
    string Content = "");
