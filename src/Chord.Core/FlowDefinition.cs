using System.Collections.Generic;

namespace Chord;

/// <summary>
/// Represents a parsed Chord flow definition and metadata extracted from YAML.
/// </summary>
public sealed record FlowDefinition(
    string Name,
    string Version,
    string CompletionQueue,
    string FailureQueue,
    IReadOnlyList<FlowStep> Steps);

/// <summary>
/// Represents a single step defined inside a flow.
/// </summary>
/// <param name="Id">Unique identifier of the step.</param>
/// <param name="CommandQueue">Queue that carries the step command.</param>
public sealed record FlowStep(string Id, string CommandQueue);
