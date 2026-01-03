using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;

namespace Chord.Messaging.RabitMQ.Messaging;

/// <summary>
/// Publishes messages to RabbitMQ queues using the shared connection provider.
/// </summary>
internal sealed class RabbitMqMessagePublisher : IRabbitMqMessagePublisher
{
    private readonly IRabbitMqConnectionProvider _connectionProvider;

    public RabbitMqMessagePublisher(IRabbitMqConnectionProvider connectionProvider)
    {
        _connectionProvider = connectionProvider ?? throw new ArgumentNullException(nameof(connectionProvider));
    }

    /// <inheritdoc />
    public ValueTask PublishAsync(string queueName, ReadOnlyMemory<byte> body, string? correlationId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(queueName))
        {
            throw new ArgumentException("Queue name cannot be null or whitespace.", nameof(queueName));
        }

        cancellationToken.ThrowIfCancellationRequested();

        using var channel = _connectionProvider.Connection.CreateModel();
        channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false);

        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            properties.CorrelationId = correlationId;
            properties.Headers ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            properties.Headers["x-correlation-id"] = correlationId;
        }

        channel.BasicPublish(exchange: string.Empty, routingKey: queueName, mandatory: false, basicProperties: properties, body: body);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Helper that publishes UTF-8 text to a queue.
    /// </summary>
    public ValueTask PublishAsync(string queueName, string message, string? correlationId = null, CancellationToken cancellationToken = default) =>
        PublishAsync(queueName, Encoding.UTF8.GetBytes(message ?? string.Empty), correlationId, cancellationToken);
}
