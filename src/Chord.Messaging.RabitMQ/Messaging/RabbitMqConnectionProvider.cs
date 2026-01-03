using System;
using System.Threading;
using System.Threading.Tasks;
using Chord.Messaging.RabitMQ.Configuration;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Chord.Messaging.RabitMQ.Messaging;

/// <summary>
/// Manages the RabbitMQ connection lifecycle used by publishers.
/// </summary>
internal sealed class RabbitMqConnectionProvider : IRabbitMqConnectionProvider
{
    private readonly IOptions<RabbitMqOptions> _options;
    private readonly Lazy<IConnection> _connection;

    public RabbitMqConnectionProvider(IOptions<RabbitMqOptions> options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _connection = new Lazy<IConnection>(CreateConnection, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <inheritdoc />
    public IConnection Connection => _connection.Value;

    public void Dispose() => DisposeConnection();

    public ValueTask DisposeAsync()
    {
        DisposeConnection();
        return ValueTask.CompletedTask;
    }

    private void DisposeConnection()
    {
        if (_connection.IsValueCreated)
        {
            Connection.Dispose();
        }
    }

    private IConnection CreateConnection()
    {
        var settings = _options.Value;

        var factory = new ConnectionFactory
        {
            HostName = settings.HostName,
            Port = settings.Port,
            UserName = settings.UserName,
            Password = settings.Password,
            VirtualHost = settings.VirtualHost,
            DispatchConsumersAsync = true,
            ClientProvidedName = settings.ClientProvidedName
        };

        if (settings.UseTls)
        {
            factory.Ssl = new SslOption
            {
                Enabled = true,
                Version = System.Security.Authentication.SslProtocols.Tls12
            };
        }

        return factory.CreateConnection();
    }
}
