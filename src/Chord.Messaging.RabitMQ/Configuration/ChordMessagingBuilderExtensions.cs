using System;
using Chord.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chord.Messaging.RabitMQ.Configuration;

/// <summary>
/// Adds messaging configuration support to the Chord configuration pipeline.
/// </summary>
public static class ChordMessagingBuilderExtensions
{
    /// <summary>
    /// Registers messaging providers (RabbitMQ/Kafka) and validates their configuration.
    /// </summary>
    /// <param name="builder">Chord configuration builder provided by <c>AddChord</c>.</param>
    /// <param name="configure">Delegate that selects the messaging provider and binds options.</param>
    public static void Messaging(this ChordConfigurationBuilder builder, Action<MessagingBuilder> configure)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        var messagingBuilder = new MessagingBuilder(builder.Services);
        configure(messagingBuilder);
        messagingBuilder.Validate();
    }
}
