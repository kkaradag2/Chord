using System;
using Chord.Core.Exceptions;
using Chord.Messaging.RabitMQ.Messaging;
using Chord.Messaging.RabitMQ.Messaging.Completion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Chord.Messaging.RabitMQ.Configuration;

/// <summary>
/// Provides a fluent API to register messaging backplanes such as RabbitMQ or Kafka.
/// </summary>
public sealed class MessagingBuilder
{
    private readonly IServiceCollection _services;
    private MessagingProviderKind? _selectedProvider;
    private bool _bindInvoked;

    internal MessagingBuilder(IServiceCollection services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <summary>
    /// Registers the RabbitMQ messaging provider and validates its configuration.
    /// </summary>
    /// <param name="configure">Delegate that populates <see cref="RabbitMqOptions"/>.</param>
    /// <exception cref="ChordConfigurationException">Thrown when configuration is invalid or another provider is already registered.</exception>
    public MessagingBuilder RabbitMq(Action<RabbitMqOptions> configure)
    {
        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        EnsureSingleProvider("RabbitMQ");

        var options = new RabbitMqOptions();
        configure(options);
        options.Validate();

        _services.AddSingleton<IOptions<RabbitMqOptions>>(_ => Options.Create(options));
        _services.AddSingleton<IRabbitMqConnectionProvider, RabbitMqConnectionProvider>();
        _services.AddSingleton<RabbitMqMessagePublisher>();
        _services.AddSingleton<IRabbitMqMessagePublisher>(sp => sp.GetRequiredService<RabbitMqMessagePublisher>());
        _services.AddSingleton<IChordMessagePublisher>(sp => sp.GetRequiredService<RabbitMqMessagePublisher>());
        _services.AddSingleton(options);

        _selectedProvider = MessagingProviderKind.RabbitMq;
        return this;
    }

    /// <summary>
    /// Placeholder for the Kafka provider configuration. Not yet supported.
    /// </summary>
    /// <exception cref="NotSupportedException">Always thrown until Kafka integration ships.</exception>
    public MessagingBuilder Kafka(Action<KafkaMessagingOptions> configure)
    {
        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        EnsureSingleProvider("Kafka");
        throw new NotSupportedException("Kafka messaging provider is not implemented yet.");
    }

    /// <summary>
    /// Finalizes messaging configuration and enforces that at least one provider is registered.
    /// </summary>
    /// <exception cref="ChordConfigurationException">Thrown when no providers were configured.</exception>
    public void BindFlow()
    {
        if (_bindInvoked)
        {
            throw new ChordConfigurationException("Messaging.BindFlow() can only be called once.");
        }

        EnsureProviderPresent();
        _services.AddSingleton<IChordFlowMessenger, ChordFlowMessenger>();
        _services.AddSingleton<IFlowCompletionProcessor, FlowCompletionProcessor>();
        _services.AddHostedService<FlowCompletionListener>();
        _bindInvoked = true;
    }

    internal void Validate()
    {
        EnsureProviderPresent();
        if (!_bindInvoked)
        {
            throw new ChordConfigurationException("Messaging configuration must call BindFlow() to finalize setup.");
        }
    }

    private void EnsureSingleProvider(string providerName)
    {
        if (_selectedProvider is not null)
        {
            throw new ChordConfigurationException($"Only one messaging provider can be registered. '{_selectedProvider}' is already configured.");
        }
    }

    private void EnsureProviderPresent()
    {
        if (_selectedProvider is null)
        {
            throw new ChordConfigurationException("At least one messaging provider must be registered before binding the flow.");
        }
    }
}
