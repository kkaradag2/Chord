using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Chord;

/// <summary>
/// Represents the persistence surface used by Chord to store orchestration state.
/// </summary>
public interface IChordStore
{
    /// <summary>
    /// Creates a flow instance entry when orchestration starts.
    /// </summary>
    Task<FlowInstanceRecord> CreateFlowInstanceAsync(string flowName, string correlationId, DateTimeOffset startedAtUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a flow instance as completed.
    /// </summary>
    Task<FlowInstanceRecord> CompleteFlowInstanceAsync(Guid flowInstanceId, DateTimeOffset completedAtUtc, long durationMilliseconds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a step instance entry when a step is dispatched.
    /// </summary>
    Task<StepInstanceRecord> CreateStepInstanceAsync(Guid flowInstanceId, string stepId, DateTimeOffset startedAtUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a step instance as completed.
    /// </summary>
    Task<StepInstanceRecord> CompleteStepInstanceAsync(Guid stepInstanceId, DateTimeOffset completedAtUtc, long durationMilliseconds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns flow instances stored in the provider for diagnostics or testing.
    /// </summary>
    Task<IReadOnlyList<FlowInstanceRecord>> QueryFlowInstancesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns step instances for a given flow.
    /// </summary>
    Task<IReadOnlyList<StepInstanceRecord>> QueryStepInstancesAsync(Guid flowInstanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a message log entry for outbound or inbound orchestration traffic.
    /// </summary>
    Task LogMessageAsync(ChordMessageDirection direction, Guid flowInstanceId, string queueName, string? stepId, IReadOnlyDictionary<string, string>? headers, DateTimeOffset createdAtUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns logged messages for a given flow instance.
    /// </summary>
    Task<IReadOnlyList<MessageLogRecord>> QueryMessageLogsAsync(Guid flowInstanceId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the persisted state of a flow instance.
/// </summary>
public sealed record FlowInstanceRecord(
    Guid Id,
    string FlowName,
    string CorrelationId,
    FlowInstanceStatus Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    long? DurationMilliseconds);

/// <summary>
/// Represents the persisted state of a step instance.
/// </summary>
public sealed record StepInstanceRecord(
    Guid Id,
    Guid FlowInstanceId,
    string StepId,
    StepInstanceStatus Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    long? DurationMilliseconds);

/// <summary>
/// Represents a persisted message envelope.
/// </summary>
public sealed record MessageLogRecord(
    Guid Id,
    Guid FlowInstanceId,
    string? StepId,
    ChordMessageDirection Direction,
    string QueueName,
    IReadOnlyDictionary<string, string>? Headers,
    DateTimeOffset CreatedAtUtc);

public enum FlowInstanceStatus
{
    Running,
    Completed
}

public enum StepInstanceStatus
{
    Running,
    Completed
}

public enum ChordMessageDirection
{
    Outbound,
    Inbound
}
