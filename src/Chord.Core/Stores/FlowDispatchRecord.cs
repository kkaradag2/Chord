using System;

namespace Chord.Core.Stores;

/// <summary>
/// Represents a single dispatch event that Chord recorded for auditing.
/// </summary>
public sealed record FlowDispatchRecord(
    string CorrelationId,
    string StepId,
    string QueueName,
    FlowDispatchStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    TimeSpan Duration,
    string Payload);

/// <summary>
/// Enumerates possible dispatch states.
/// </summary>
public enum FlowDispatchStatus
{
    InProgress = 0,
    Completed = 1,
    Failed = 2
}
