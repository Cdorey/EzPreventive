namespace EzNutrition.Application.Ports;

/// <summary>Base type for failures reported by an AI advice host adapter.</summary>
public abstract class AiAdviceException : Exception
{
    protected AiAdviceException(string message)
        : base(message)
    {
    }

    protected AiAdviceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Identifies a host-independent AI capability access failure.</summary>
public enum AiAdviceAccessFailureKind
{
    /// <summary>The capability could not be reached or read.</summary>
    Unavailable = 0,

    /// <summary>The current caller is not allowed to use the capability.</summary>
    AccessDenied = 1,

    /// <summary>The host rejected an otherwise valid request.</summary>
    Rejected = 2
}

/// <summary>Represents a failure while accessing the host's AI capability.</summary>
public sealed class AiAdviceAccessException : AiAdviceException
{
    public AiAdviceAccessException(
        string message,
        AiAdviceAccessFailureKind failureKind = AiAdviceAccessFailureKind.Unavailable)
        : base(message)
    {
        FailureKind = failureKind;
    }

    public AiAdviceAccessException(
        string message,
        Exception innerException,
        AiAdviceAccessFailureKind failureKind = AiAdviceAccessFailureKind.Unavailable)
        : base(message, innerException)
    {
        FailureKind = failureKind;
    }

    /// <summary>Gets the host-independent reason the capability could not be accessed.</summary>
    public AiAdviceAccessFailureKind FailureKind { get; }
}

/// <summary>Represents an error explicitly returned by the configured AI provider.</summary>
public sealed class AiAdviceProviderException : AiAdviceException
{
    public AiAdviceProviderException(string message)
        : base(message)
    {
    }
}

/// <summary>Represents an invalid or incomplete response from the host adapter.</summary>
public sealed class AiAdviceProtocolException : AiAdviceException
{
    public AiAdviceProtocolException(string message)
        : base(message)
    {
    }

    public AiAdviceProtocolException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
