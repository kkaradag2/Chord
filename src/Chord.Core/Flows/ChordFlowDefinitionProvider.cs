using System;

namespace Chord.Core.Flows;

/// <summary>
/// Default <see cref="IChordFlowDefinitionProvider"/> implementation that simply stores the parsed flow.
/// </summary>
internal sealed class ChordFlowDefinitionProvider : IChordFlowDefinitionProvider
{
    /// <summary>
    /// Initializes the provider with the flow definition captured during AddChord configuration.
    /// </summary>
    /// <param name="flow">Validated workflow definition.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="flow"/> is null.</exception>
    public ChordFlowDefinitionProvider(ChordFlowDefinition flow)
    {
        Flow = flow ?? throw new ArgumentNullException(nameof(flow));
    }

    /// <summary>
    /// Gets the workflow definition; consumers should treat it as read-only metadata.
    /// </summary>
    public ChordFlowDefinition Flow { get; }
}
