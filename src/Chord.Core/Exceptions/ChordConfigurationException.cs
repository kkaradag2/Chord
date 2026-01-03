using System;

namespace Chord.Core.Exceptions;

/// <summary>
/// Represents configuration problems detected while setting up Chord.
/// </summary>
public sealed class ChordConfigurationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the exception with a descriptive message.
    /// </summary>
    /// <param name="message">Explanation of what configuration issue occurred.</param>
    public ChordConfigurationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance that wraps another exception for additional context.
    /// </summary>
    /// <param name="message">Explanation of what configuration issue occurred.</param>
    /// <param name="innerException">Underlying exception that triggered the configuration failure.</param>
    public ChordConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
