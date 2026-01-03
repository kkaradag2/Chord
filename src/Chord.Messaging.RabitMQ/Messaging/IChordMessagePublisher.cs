using System;
using System.Threading;
using System.Threading.Tasks;

namespace Chord.Messaging.RabitMQ.Messaging;

/// <summary>
/// Describes a component capable of publishing messages to the configured messaging infrastructure.
/// </summary>
public interface IChordMessagePublisher
{
    /// <summary>
    /// Publishes raw bytes to the given queue.
    /// </summary>
    ValueTask PublishAsync(string queueName, ReadOnlyMemory<byte> body, CancellationToken cancellationToken = default);
}
