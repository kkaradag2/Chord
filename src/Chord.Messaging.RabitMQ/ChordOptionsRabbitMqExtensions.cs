using Chord;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Chord.Messaging.RabitMQ;

public static class ChordOptionsRabbitMqExtensions
{
    private const string ProviderName = "RabbitMQ";

    public static ChordOptions UseRabbitMq(this ChordOptions options, Action<RabbitMqOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configure);

        var rabbitOptions = new RabbitMqOptions();
        configure(rabbitOptions);

        ValidateOptions(rabbitOptions);

        options.RegisterMessagingProvider(ProviderName, services =>
        {
            var snapshot = Clone(rabbitOptions);
            services.AddSingleton<RabbitMqOptions>(_ => snapshot);
            services.AddSingleton<IOptions<RabbitMqOptions>>(_ => Options.Create(snapshot));
            services.AddSingleton<IChordMessageBus, RabbitMqMessageBus>();
        });

        return options;
    }

    private static RabbitMqOptions Clone(RabbitMqOptions source)
    {
        return new RabbitMqOptions
        {
            HostName = source.HostName,
            Port = source.Port,
            UserName = source.UserName,
            Password = source.Password,
            VirtualHost = source.VirtualHost,
            UseSsl = source.UseSsl
        };
    }

    private static void ValidateOptions(RabbitMqOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.HostName))
        {
            throw new ChordConfigurationException(ProviderName, "RabbitMQ host name must be provided.");
        }

        if (options.Port <= 0)
        {
            throw new ChordConfigurationException(ProviderName, "RabbitMQ port must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(options.UserName))
        {
            throw new ChordConfigurationException(ProviderName, "RabbitMQ username must be provided.");
        }

        if (string.IsNullOrWhiteSpace(options.Password))
        {
            throw new ChordConfigurationException(ProviderName, "RabbitMQ password must be provided.");
        }

        if (string.IsNullOrWhiteSpace(options.VirtualHost))
        {
            throw new ChordConfigurationException(ProviderName, "RabbitMQ virtual host must be provided.");
        }
    }
}
