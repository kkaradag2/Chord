using RabbitMQ.Client;
using Xunit;

namespace Chord.Test.Messaging.Basic;

/// <summary>
/// Custom Fact attribute that skips automatically when RabbitMQ is not reachable.
/// </summary>
public sealed class RabbitMqFactAttribute : FactAttribute
{
    private static bool? _cachedAvailability;

    public RabbitMqFactAttribute()
    {
        if (!IsRabbitMqAvailable())
        {
            Skip = "Requires RabbitMQ at localhost:5672 (guest/guest).";
        }
    }

    private static bool IsRabbitMqAvailable()
    {
        if (_cachedAvailability.HasValue)
        {
            return _cachedAvailability.Value;
        }

        try
        {
            var factory = new ConnectionFactory
            {
                HostName = "localhost",
                Port = 5672,
                UserName = "guest",
                Password = "guest"
            };

            using var connection = factory.CreateConnection();
            _cachedAvailability = connection.IsOpen;
        }
        catch
        {
            _cachedAvailability = false;
        }

        return _cachedAvailability.Value;
    }
}
