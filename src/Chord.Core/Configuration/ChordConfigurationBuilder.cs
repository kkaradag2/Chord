using System;
using Chord.Core.Exceptions;
using Chord.Core.Flows;
using Microsoft.Extensions.DependencyInjection;

namespace Chord.Core.Configuration;

/// <summary>
/// Root configuration builder invoked by host applications to configure Chord services.
/// </summary>
public sealed class ChordConfigurationBuilder
{
    private readonly IServiceCollection _services;
    private ChordFlowDefinition? _flowDefinition;

    /// <summary>
    /// Initializes the builder with the service collection so extensions can register components.
    /// </summary>
    /// <param name="services">Host service collection that Chord augments.</param>
    public ChordConfigurationBuilder(IServiceCollection services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <summary>
    /// Gets the underlying service collection for advanced integrations.
    /// </summary>
    public IServiceCollection Services => _services;

    /// <summary>
    /// Configures the workflow definition that Chord should use.
    /// </summary>
    /// <param name="configure">Callback that selects and loads a workflow definition via <see cref="FlowBuilder"/>.</param>
    public void Flow(Action<FlowBuilder> configure)
    {
        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        var flowBuilder = new FlowBuilder(FlowDefinitionLoader.FromYamlFile);
        configure(flowBuilder);
        _flowDefinition = flowBuilder.Build();
    }

    /// <summary>
    /// Gets the configured flow definition or throws if none has been provided.
    /// </summary>
    internal ChordFlowDefinition Build()
    {
        return _flowDefinition ?? throw new ChordConfigurationException("Chord flow must be configured by calling Flow().");
    }
}
