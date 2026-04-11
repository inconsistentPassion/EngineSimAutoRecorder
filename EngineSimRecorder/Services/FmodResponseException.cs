using System;

namespace EngineSimRecorder.Services;

/// <summary>
/// Exception thrown when FMOD Studio returns an error response via the TCP automation port.
/// </summary>
public sealed class FmodResponseException : Exception
{
    public FmodResponseException(string message) : base(message)
    {
    }

    public FmodResponseException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
