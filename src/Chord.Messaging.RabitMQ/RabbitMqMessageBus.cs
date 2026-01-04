using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Chord;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Chord.Messaging.RabitMQ;

/// <summary>
/// RabbitMQ-backed implementation of <see cref="IChordMessageBus"/>.
/// </summary>
public sealed class RabbitMqMessageBus : IChordMessageBus, IDisposable
{
    private readonly ConnectionFactory _connectionFactory;
    private readonly ILogger<RabbitMqMessageBus> _logger;
    private IConnection? _connection;
    private IModel? _channel;
    private readonly object _syncLock = new();

    public RabbitMqMessageBus(IOptions<RabbitMqOptions> options, ILogger<RabbitMqMessageBus> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        var cfg = options.Value;
        _connectionFactory = RabbitMqConnectionFactoryBuilder.Build(cfg);
        _logger = logger;
    }

    public Task PublishAsync(string queueName, ReadOnlyMemory<byte> payload, IReadOnlyDictionary<string, string>? headers = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(queueName))
        {
            throw new ArgumentException("Queue name cannot be null or whitespace.", nameof(queueName));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var channel = EnsureChannel();
        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;

        if (headers is { Count: > 0 })
        {
            properties.Headers ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in headers)
            {
                properties.Headers[header.Key] = header.Value;
            }
        }

        channel.BasicPublish(exchange: string.Empty, routingKey: queueName, basicProperties: properties, body: payload);
        _logger.LogTrace("Published message to queue {QueueName}", queueName);

        return Task.CompletedTask;
    }

    public Task SubscribeAsync(string queueName, Func<ChordMessage, Task> handler, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(queueName))
        {
            throw new ArgumentException("Queue name cannot be null or whitespace.", nameof(queueName));
        }
        ArgumentNullException.ThrowIfNull(handler);

        var connection = _connectionFactory.CreateConnection();
        var channel = connection.CreateModel();
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.Received += async (sender, args) =>
        {
            var headers = ExtractHeaders(args.BasicProperties);
            var body = args.Body.ToArray();
            var message = new ChordMessage(queueName, body, headers);
            await handler(message).ConfigureAwait(false);
        };

        var consumerTag = channel.BasicConsume(queue: queueName, autoAck: true, consumer: consumer);
        cancellationToken.Register(() =>
        {
            try
            {
                channel.BasicCancel(consumerTag);
                channel.Close();
            }
            catch
            {
            }
            finally
            {
                channel.Dispose();
                connection.Dispose();
            }
        });

        return Task.CompletedTask;
    }

    private IModel EnsureChannel()
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        lock (_syncLock)
        {
            if (_channel is { IsOpen: true })
            {
                return _channel;
            }

            DisposeChannel();
            _connection = _connectionFactory.CreateConnection();
            _channel = _connection.CreateModel();
            return _channel;
        }
    }

    private void DisposeChannel()
    {
        try
        {
            _channel?.Close();
        }
        catch
        {
            // Ignore exceptions during shutdown.
        }
        finally
        {
            _channel?.Dispose();
            _channel = null;
        }

        _connection?.Dispose();
        _connection = null;
    }

    public void Dispose()
    {
        DisposeChannel();
    }

    private static IReadOnlyDictionary<string, string> ExtractHeaders(IBasicProperties properties)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (properties.Headers is not null)
        {
            foreach (var entry in properties.Headers)
            {
                if (entry.Value is byte[] bytes)
                {
                    headers[entry.Key] = Encoding.UTF8.GetString(bytes);
                }
                else if (entry.Value is ReadOnlyMemory<byte> memory)
                {
                    headers[entry.Key] = Encoding.UTF8.GetString(memory.ToArray());
                }
                else if (entry.Value is string s)
                {
                    headers[entry.Key] = s;
                }
                else if (entry.Value is null)
                {
                    headers[entry.Key] = string.Empty;
                }
                else
                {
                    headers[entry.Key] = entry.Value.ToString() ?? string.Empty;
                }
            }
        }

        return headers;
    }
}
