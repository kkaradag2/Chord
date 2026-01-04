using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Chord;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

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
        _connectionFactory = new ConnectionFactory
        {
            HostName = cfg.HostName,
            Port = cfg.Port,
            UserName = cfg.UserName,
            Password = cfg.Password,
            VirtualHost = cfg.VirtualHost,
            DispatchConsumersAsync = true
        };

        if (cfg.UseSsl)
        {
            _connectionFactory.Ssl = new SslOption
            {
                Enabled = true
            };
        }

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
}
