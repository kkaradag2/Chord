using System;
using System;
using System.IO;
using Chord.Core;
using Chord.Core.Exceptions;
using Chord.Messaging.RabitMQ.Configuration;
using Chord.Messaging.RabitMQ.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Chord.Test.Registrations;

public class MessagingConfigurationTests
{
    private static string SamplesDirectory =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SamplesYamls"));

    private static string Sample(string fileName) => Path.Combine(SamplesDirectory, fileName);

    /// <summary>
    /// Proves that configuring RabbitMQ registers the expected publisher services in DI.
    /// </summary>
    [Fact]
    public void Messaging_WithRabbitMq_RegistersPublisher()
    {
        var services = new ServiceCollection();

        services.AddChord(config =>
        {
            config.Flow(flow => flow.FromYamlFile(Sample("order-flow.yaml")));
            config.Messaging(m =>
            {
                m.RabbitMq(options =>
                {
                    options.HostName = "localhost";
                    options.UserName = "guest";
                    options.Password = "guest";
                });

                m.BindFlow();
            });
        });

        using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IRabbitMqMessagePublisher>();
        var messenger = provider.GetRequiredService<IChordFlowMessenger>();

        Assert.NotNull(publisher);
        Assert.NotNull(messenger);
    }

    /// <summary>
    /// Ensures the configuration fails fast when BindFlow() is omitted.
    /// </summary>
    [Fact]
    public void Messaging_WithoutBindFlow_Throws()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<ChordConfigurationException>(() =>
        {
            services.AddChord(config =>
            {
                config.Flow(flow => flow.FromYamlFile(Sample("order-flow.yaml")));
                config.Messaging(m => m.RabbitMq(options =>
                {
                    options.HostName = "localhost";
                    options.UserName = "guest";
                    options.Password = "guest";
                }));
            });
        });

        Assert.Contains("BindFlow", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that attempting to register more than one provider triggers a configuration error.
    /// </summary>
    [Fact]
    public void Messaging_MultipleProviders_Throws()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<ChordConfigurationException>(() =>
        {
            services.AddChord(config =>
            {
                config.Flow(flow => flow.FromYamlFile(Sample("order-flow.yaml")));
                config.Messaging(m =>
                {
                    m.RabbitMq(options =>
                    {
                        options.HostName = "localhost";
                        options.UserName = "guest";
                        options.Password = "guest";
                    });

                    m.Kafka(_ => { });
                    m.BindFlow();
                });
            });
        });

        Assert.Contains("only one messaging provider", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Ensures BindFlow cannot be invoked more than once.
    /// </summary>
    [Fact]
    public void Messaging_DoubleBindFlow_Throws()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<ChordConfigurationException>(() =>
        {
            services.AddChord(config =>
            {
                config.Flow(flow => flow.FromYamlFile(Sample("order-flow.yaml")));
                config.Messaging(m =>
                {
                    m.RabbitMq(options =>
                    {
                        options.HostName = "localhost";
                        options.UserName = "guest";
                        options.Password = "guest";
                    });

                    m.BindFlow();
                    m.BindFlow();
                });
            });
        });

        Assert.Contains("only be called once", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
