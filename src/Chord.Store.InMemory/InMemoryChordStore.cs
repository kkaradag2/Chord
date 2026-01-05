using System.Collections.Concurrent;
using Chord;

namespace Chord.Store.InMemory;

/// <summary>
/// Basic in-memory store intended for development and testing scenarios.
/// Mirrors the persistence semantics expected by <see cref="IChordStore"/>.
/// </summary>
public sealed class InMemoryChordStore : IChordStore
{
    private readonly ConcurrentDictionary<Guid, FlowInstanceRecord> _flows = new();
    private readonly ConcurrentDictionary<Guid, StepInstanceRecord> _steps = new();
    private readonly ConcurrentDictionary<Guid, MessageLogRecord> _messageLogs = new();

    public Task<FlowInstanceRecord> CreateFlowInstanceAsync(string flowName, string correlationId, DateTimeOffset startedAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flowName);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        var record = new FlowInstanceRecord(Guid.NewGuid(), flowName, correlationId, FlowInstanceStatus.Running, startedAtUtc, null, null);
        if (!_flows.TryAdd(record.Id, record))
        {
            throw new InvalidOperationException($"Flow instance '{record.Id}' already exists.");
        }

        return Task.FromResult(record);
    }

    public Task<FlowInstanceRecord> CompleteFlowInstanceAsync(Guid flowInstanceId, DateTimeOffset completedAtUtc, long durationMilliseconds, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(UpdateFlow(flowInstanceId, flow =>
        {
            return flow with
            {
                Status = FlowInstanceStatus.Completed,
                CompletedAtUtc = completedAtUtc,
                DurationMilliseconds = durationMilliseconds
            };
        }));
    }

    public Task<StepInstanceRecord> CreateStepInstanceAsync(Guid flowInstanceId, string stepId, DateTimeOffset startedAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stepId);

        if (!_flows.ContainsKey(flowInstanceId))
        {
            throw new InvalidOperationException($"Flow instance '{flowInstanceId}' is not known by the in-memory store.");
        }

        var record = new StepInstanceRecord(Guid.NewGuid(), flowInstanceId, stepId, StepInstanceStatus.Running, startedAtUtc, null, null);
        if (!_steps.TryAdd(record.Id, record))
        {
            throw new InvalidOperationException($"Step instance '{record.Id}' already exists.");
        }

        return Task.FromResult(record);
    }

    public Task<StepInstanceRecord> CompleteStepInstanceAsync(Guid stepInstanceId, DateTimeOffset completedAtUtc, long durationMilliseconds, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(UpdateStep(stepInstanceId, step =>
        {
            return step with
            {
                Status = StepInstanceStatus.Completed,
                CompletedAtUtc = completedAtUtc,
                DurationMilliseconds = durationMilliseconds
            };
        }));
    }

    public Task<IReadOnlyList<FlowInstanceRecord>> QueryFlowInstancesAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = _flows.Values
            .OrderBy(flow => flow.StartedAtUtc)
            .ToArray();

        return Task.FromResult<IReadOnlyList<FlowInstanceRecord>>(snapshot);
    }

    public Task<IReadOnlyList<StepInstanceRecord>> QueryStepInstancesAsync(Guid flowInstanceId, CancellationToken cancellationToken = default)
    {
        var snapshot = _steps.Values
            .Where(step => step.FlowInstanceId == flowInstanceId)
            .OrderBy(step => step.StartedAtUtc)
            .ToArray();

        return Task.FromResult<IReadOnlyList<StepInstanceRecord>>(snapshot);
    }

    public Task LogMessageAsync(ChordMessageDirection direction, Guid flowInstanceId, string queueName, string? stepId, IReadOnlyDictionary<string, string>? headers, DateTimeOffset createdAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        var record = new MessageLogRecord(
            Guid.NewGuid(),
            flowInstanceId,
            stepId,
            direction,
            queueName,
            headers is null ? null : new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase),
            createdAtUtc);

        if (!_messageLogs.TryAdd(record.Id, record))
        {
            throw new InvalidOperationException($"Message log '{record.Id}' already exists.");
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<MessageLogRecord>> QueryMessageLogsAsync(Guid flowInstanceId, CancellationToken cancellationToken = default)
    {
        var snapshot = _messageLogs.Values
            .Where(log => log.FlowInstanceId == flowInstanceId)
            .OrderBy(log => log.CreatedAtUtc)
            .ToArray();

        return Task.FromResult<IReadOnlyList<MessageLogRecord>>(snapshot);
    }

    private FlowInstanceRecord UpdateFlow(Guid id, Func<FlowInstanceRecord, FlowInstanceRecord> updater)
    {
        while (true)
        {
            if (!_flows.TryGetValue(id, out var current))
            {
                throw new InvalidOperationException($"Flow instance '{id}' does not exist.");
            }

            var updated = updater(current);
            if (_flows.TryUpdate(id, updated, current))
            {
                return updated;
            }
        }
    }

    private StepInstanceRecord UpdateStep(Guid id, Func<StepInstanceRecord, StepInstanceRecord> updater)
    {
        while (true)
        {
            if (!_steps.TryGetValue(id, out var current))
            {
                throw new InvalidOperationException($"Step instance '{id}' does not exist.");
            }

            var updated = updater(current);
            if (_steps.TryUpdate(id, updated, current))
            {
                return updated;
            }
        }
    }
}
