using System;
using Chord.Core.Exceptions;

namespace Chord.Messaging.RabitMQ.Configuration;

/// <summary>
/// Defines the RabbitMQ connection parameters used by Chord messaging.
/// </summary>
public sealed class RabbitMqOptions
{
    /// <summary>
    /// Gets or sets the RabbitMQ host name.
    /// </summary>
    public string HostName { get; set; } = "localhost";

    /// <summary>
    /// Gets or sets the TCP port for the broker.
    /// </summary>
    public int Port { get; set; } = 5672;

    /// <summary>
    /// Gets or sets the username used to authenticate against RabbitMQ.
    /// </summary>
    public string UserName { get; set; } = "guest";

    /// <summary>
    /// Gets or sets the password used to authenticate against RabbitMQ.
    /// </summary>
    public string Password { get; set; } = "guest";

    /// <summary>
    /// Gets or sets the virtual host that encapsulates queues/exchanges.
    /// </summary>
    public string VirtualHost { get; set; } = "/";

    /// <summary>
    /// Gets or sets an optional client name that appears in RabbitMQ management UI.
    /// </summary>
    public string? ClientProvidedName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether TLS should be used.
    /// </summary>
    public bool UseTls { get; set; }

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(HostName))
        {
            throw new ChordConfigurationException("RabbitMQ configuration requires a HostName.");
        }

        if (Port <= 0)
        {
            throw new ChordConfigurationException("RabbitMQ configuration requires a valid TCP Port.");
        }

        if (string.IsNullOrWhiteSpace(UserName))
        {
            throw new ChordConfigurationException("RabbitMQ configuration requires a UserName.");
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            throw new ChordConfigurationException("RabbitMQ configuration requires a Password.");
        }

        if (string.IsNullOrWhiteSpace(VirtualHost))
        {
            throw new ChordConfigurationException("RabbitMQ configuration requires a VirtualHost.");
        }
    }
}
