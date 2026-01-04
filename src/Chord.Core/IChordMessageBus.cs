using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Chord;

/// <summary>
/// Provides messaging capabilities for Chord orchestrations.
/// </summary>
public interface IChordMessageBus
{
    /// <summary>
    /// Publishes a message to the specified queue.
    /// </summary>
    /// <param name="queueName">Destination queue.</param>
    /// <param name="payload">Serialized payload.</param>
    /// <param name="headers">Optional headers that should accompany the message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync(string queueName, ReadOnlyMemory<byte> payload, IReadOnlyDictionary<string, string>? headers = null, CancellationToken cancellationToken = default);
}
