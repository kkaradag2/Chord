using System;
using Chord.Core.Exceptions;
using Chord.Core.Flows;

namespace Chord.Core.Configuration;

/// <summary>
/// Fluent API entry point for loading a workflow definition from supported sources.
/// </summary>
public sealed class FlowBuilder
{
    private readonly Func<string, ChordFlowDefinition> _loader;
    private ChordFlowDefinition? _flowDefinition;

    internal FlowBuilder(Func<string, ChordFlowDefinition> loader)
    {
        _loader = loader;
    }

    /// <summary>
    /// Loads the workflow definition from the provided YAML file path.
    /// </summary>
    /// <param name="filePath">Relative or absolute path to a YAML file.</param>
    /// <returns>The same <see cref="FlowBuilder"/> for fluent chaining.</returns>
    public FlowBuilder FromYamlFile(string filePath)
    {
        _flowDefinition = _loader(filePath);
        return this;
    }

    /// <summary>
    /// Returns the parsed flow definition or throws if no source has been configured.
    /// </summary>
    internal ChordFlowDefinition Build() =>
        _flowDefinition ?? throw new ChordConfigurationException("Flow definition must be provided via FromYamlFile.");
}
