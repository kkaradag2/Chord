namespace Chord.Core.Flows;

/// <summary>
/// Exposes the active <see cref="ChordFlowDefinition"/> so dependent services can inspect runtime contracts.
/// </summary>
public interface IChordFlowDefinitionProvider
{
    /// <summary>
    /// Gets the validated workflow definition loaded during host startup.
    /// </summary>
    ChordFlowDefinition Flow { get; }
}
