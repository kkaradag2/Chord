namespace Chord.Messaging.RabitMQ.Configuration;

/// <summary>
/// Enumerates the messaging providers that Chord can bind to.
/// </summary>
internal enum MessagingProviderKind
{
    RabbitMq,
    Kafka
}
