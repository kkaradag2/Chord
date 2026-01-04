namespace Chord.Messaging.Kafka;

/// <summary>
/// Represents the minimal configuration required for Kafka integration.
/// </summary>
public sealed class KafkaOptions
{
    public string BootstrapServers { get; set; } = string.Empty;

    public string DefaultTopic { get; set; } = string.Empty;
}
