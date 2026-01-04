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

    /// <summary>
    /// Subscribes to a queue and invokes the handler for each received message.
    /// </summary>
    /// <param name="queueName">Queue to listen on.</param>
    /// <param name="handler">Delegate invoked per message.</param>
    /// <param name="cancellationToken">Cancellation token that stops the subscription.</param>
    Task SubscribeAsync(string queueName, Func<ChordMessage, Task> handler, CancellationToken cancellationToken);
}

/// <summary>
/// Envelope describing a message handled by the orchestration runtime.
/// </summary>
public sealed record ChordMessage(string QueueName, ReadOnlyMemory<byte> Body, IReadOnlyDictionary<string, string> Headers);

/// <summary>
/// Well-known message header names used by Chord.
/// </summary>
public static class ChordMessageHeaders
{
    public const string CorrelationId = "x-correlation-id";
    public const string StepId = "x-step-id";
}
