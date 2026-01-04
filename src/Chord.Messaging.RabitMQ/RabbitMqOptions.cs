namespace Chord.Messaging.RabitMQ;

/// <summary>
/// Represents configuration values required to connect to RabbitMQ.
/// </summary>
public sealed class RabbitMqOptions
{
    public string HostName { get; set; } = string.Empty;

    public int Port { get; set; } = 5672;

    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string VirtualHost { get; set; } = "/";

    public bool UseSsl { get; set; }
}
