using Chord;
using Chord.Messaging.Kafka;
using Chord.Messaging.RabitMQ;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Chord.Tests.Registration;

public class MessagingProviderConfigurationTests
{
    [Fact]
    public void AddChord_WithValidRabbitMqConfiguration_RegistersMessageBus()
    {
        var services = CreateServiceCollection();

        services.AddChord(options =>
        {
            options.UseRabbitMq(rabbit =>
            {
                rabbit.HostName = "localhost";
                rabbit.Port = 5672;
                rabbit.UserName = "guest";
                rabbit.Password = "guest";
                rabbit.VirtualHost = "/";
            });

            options.UseInMemoryStore();
        });

        using var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IChordMessageBus>();

        Assert.IsType<RabbitMqMessageBus>(bus);
    }

    [Fact]
    public void AddChord_WithInvalidRabbitMqConfiguration_Throws()
    {
        var services = CreateServiceCollection();

        var ex = Assert.Throws<ChordConfigurationException>(() =>
        {
            services.AddChord(options =>
            {
                options.UseRabbitMq(rabbit =>
                {
                    rabbit.Port = 0;
                });

                options.UseInMemoryStore();
            });
        });

        Assert.Equal("Chord configuration error for 'RabbitMQ': RabbitMQ host name must be provided.", ex.Message);
    }

    [Fact]
    public void AddChord_WithInaccessibleRabbitMqServer_Throws()
    {
        var services = CreateServiceCollection();

        var ex = Assert.Throws<ChordConfigurationException>(() =>
        {
            services.AddChord(options =>
            {
                options.UseRabbitMq(rabbit =>
                {
                    rabbit.HostName = "localhost2";
                    rabbit.Port = 5672;
                    rabbit.UserName = "guest";
                    rabbit.Password = "guest";
                    rabbit.VirtualHost = "/";
                });

                options.UseInMemoryStore();
            });
        });

        Assert.Equal("Chord configuration error for 'RabbitMQ': The messaging service (RabbitMQ) is inaccessible.", ex.Message);
    }

    [Fact]
    public void AddChord_WithValidKafkaConfiguration_RegistersPlaceholderBus()
    {
        var services = CreateServiceCollection();

        services.AddChord(options =>
        {
            options.UseKafka(kafka =>
            {
                kafka.BootstrapServers = "localhost:9092";
                kafka.DefaultTopic = "orders";
            });

            options.UseInMemoryStore();
        });

        using var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IChordMessageBus>();

        Assert.IsType<KafkaMessageBus>(bus);
    }

    [Fact]
    public void AddChord_WithMultipleMessagingProviders_Throws()
    {
        var services = CreateServiceCollection();

        var ex = Assert.Throws<ChordConfigurationException>(() =>
        {
            services.AddChord(options =>
            {
                options.UseRabbitMq(rabbit =>
                {
                    rabbit.HostName = "localhost";
                    rabbit.Port = 5672;
                    rabbit.UserName = "guest";
                    rabbit.Password = "guest";
                    rabbit.VirtualHost = "/";
                });

                options.UseKafka(kafka =>
                {
                    kafka.BootstrapServers = "localhost:9092";
                    kafka.DefaultTopic = "orders";
                });

                options.UseInMemoryStore();
            });
        });

        Assert.Equal("Chord configuration error for '(messaging)': Exactly one messaging provider must be configured, but 2 were provided (RabbitMQ, Kafka).", ex.Message);
    }

    [Fact]
    public void AddChord_WithoutMessagingProvider_Throws()
    {
        var services = CreateServiceCollection();

        var ex = Assert.Throws<ChordConfigurationException>(() =>
        {
            services.AddChord(options =>
            {
                options.UseInMemoryStore();
            });
        });

        Assert.Equal("Chord configuration error for '(messaging)': Exactly one messaging provider must be configured via UseRabbitMq or UseKafka.", ex.Message);
    }

    private static ServiceCollection CreateServiceCollection()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return services;
    }
}
