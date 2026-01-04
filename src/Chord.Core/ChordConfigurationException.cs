using System;

namespace Chord;

/// <summary>
/// Represents a configuration-time failure that should prevent the host from starting.
/// </summary>
public sealed class ChordConfigurationException : Exception
{
    public ChordConfigurationException(string resourcePath, string reason, Exception? innerException = null)
        : base($"Chord configuration error for '{resourcePath}': {reason}", innerException)
    {
        ResourcePath = resourcePath;
        Reason = reason;
    }

    /// <summary>
    /// Gets the resource path that triggered the error.
    /// </summary>
    public string ResourcePath { get; }

    /// <summary>
    /// Gets the human-readable reason for the configuration failure.
    /// </summary>
    public string Reason { get; }
}
