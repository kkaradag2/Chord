using Chord;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Chord.Messaging.Kafka;

public static class ChordOptionsKafkaExtensions
{
    private const string ProviderName = "Kafka";

    public static ChordOptions UseKafka(this ChordOptions options, Action<KafkaOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configure);

        var kafkaOptions = new KafkaOptions();
        configure(kafkaOptions);

        Validate(kafkaOptions);

        options.RegisterMessagingProvider(ProviderName, services =>
        {
            var snapshot = Clone(kafkaOptions);
            services.AddSingleton<KafkaOptions>(_ => snapshot);
            services.AddSingleton<IOptions<KafkaOptions>>(_ => Options.Create(snapshot));
            services.AddSingleton<IChordMessageBus, KafkaMessageBus>();
        });

        return options;
    }

    private static void Validate(KafkaOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.BootstrapServers))
        {
            throw new ChordConfigurationException(ProviderName, "Kafka bootstrap servers must be provided.");
        }

        if (string.IsNullOrWhiteSpace(options.DefaultTopic))
        {
            throw new ChordConfigurationException(ProviderName, "Kafka default topic must be provided.");
        }
    }

    private static KafkaOptions Clone(KafkaOptions source)
    {
        return new KafkaOptions
        {
            BootstrapServers = source.BootstrapServers,
            DefaultTopic = source.DefaultTopic
        };
    }
}
