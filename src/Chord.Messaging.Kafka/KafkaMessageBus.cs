using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Chord;
using Microsoft.Extensions.Logging;

namespace Chord.Messaging.Kafka;

/// <summary>
/// Placeholder Kafka message bus that will be implemented in a future iteration.
/// </summary>
public sealed class KafkaMessageBus : IChordMessageBus
{
    private readonly ILogger<KafkaMessageBus> _logger;

    public KafkaMessageBus(ILogger<KafkaMessageBus> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task PublishAsync(string queueName, ReadOnlyMemory<byte> payload, IReadOnlyDictionary<string, string>? headers = null, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Kafka message bus has not been implemented yet. Attempted to send to {QueueName}.", queueName);
        throw new NotSupportedException("Kafka message bus is not implemented yet.");
    }

    public Task SubscribeAsync(string queueName, Func<ChordMessage, Task> handler, CancellationToken cancellationToken)
    {
        _logger.LogWarning("Kafka message bus subscription requested for {QueueName}, but Kafka integration is not implemented.", queueName);
        throw new NotSupportedException("Kafka message bus is not implemented yet.");
    }
}
