
namespace Chord.Core.Flows;

/// <summary>
/// Represents the workflow that Chord will orchestrate, including metadata and the ordered list of steps.
/// </summary>
/// <param name="Name">Unique workflow name that appears in diagnostics and metrics.</param>
/// <param name="Version">Version string used to differentiate changes in flow definition.</param>
/// <param name="Steps">Ordered collection of steps that Chord executes sequentially.</param>
public sealed record ChordFlowDefinition(string Name, string Version, IReadOnlyList<ChordFlowStep> Steps);

/// <summary>
/// Defines a single workflow step including its command, completion, and optional rollback behavior.
/// </summary>
/// <param name="Id">Unique identifier used for referencing this step in rollbacks.</param>
/// <param name="Service">Service responsible for handling the step.</param>
/// <param name="Command">Command message emitted to start the step.</param>
/// <param name="Completion">Expected completion event metadata.</param>
/// <param name="Rollbacks">Rollbacks triggered if this step fails.</param>
public sealed record ChordFlowStep(
    string Id,
    string Service,
    FlowCommand Command,
    FlowCompletion Completion,
    IReadOnlyList<FlowRollback> Rollbacks);

/// <summary>
/// Represents a message that Chord issues to instruct a service to perform work.
/// </summary>
/// <param name="Event">Event name published to the bus.</param>
/// <param name="Queue">Queue or topic from which the service consumes commands.</param>
public sealed record FlowCommand(string Event, string Queue);

/// <summary>
/// Describes the event Chord waits for to mark a step complete including timeout semantics.
/// </summary>
/// <param name="Event">Event name that confirms the step finished.</param>
/// <param name="Timeout">Duration Chord waits before considering the step failed.</param>
/// <param name="Queue">Optional channel to listen for completion events; falls back to defaults when null.</param>
public sealed record FlowCompletion(string Event, string Timeout, string? Queue);

/// <summary>
/// Captures the compensating action that runs if a step or one of its dependents fails.
/// </summary>
/// <param name="Step">Identifier of the step to compensate.</param>
/// <param name="Command">Command emitted to undo the step.</param>
public sealed record FlowRollback(string Step, FlowCommand Command);
