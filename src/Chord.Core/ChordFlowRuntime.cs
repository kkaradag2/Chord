using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Chord;

/// <summary>
/// Provides the runtime entry point for starting flows from the host application.
/// </summary>
public sealed class ChordFlowRuntime : IHostedService, IAsyncDisposable
{
    private readonly IChordMessageBus _messageBus;
    private readonly IChordStore _store;
    private readonly ILogger<ChordFlowRuntime> _logger;
    private readonly ConcurrentDictionary<string, FlowDefinition> _flows;
    private readonly ConcurrentDictionary<string, FlowExecution> _executions = new();
    private readonly HashSet<string> _completionQueues;
    private CancellationTokenSource? _listenerCts;

    public ChordFlowRuntime(ChordOptions options, IChordMessageBus messageBus, IChordStore store, ILogger<ChordFlowRuntime> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _flows = new ConcurrentDictionary<string, FlowDefinition>(
            options.YamlFlows.ToDictionary(flow => flow.Flow.Name, flow => flow.Flow, StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);

        _completionQueues = options.YamlFlows
            .Select(flow => flow.Flow.CompletionQueue)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Starts a flow by sending the initial payload to the first step.
    /// </summary>
    public async Task StartFlowAsync(string flowName, object payload, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(flowName))
        {
            throw new ArgumentException("Flow name cannot be null or whitespace.", nameof(flowName));
        }

        ArgumentNullException.ThrowIfNull(payload);

        if (!_flows.TryGetValue(flowName, out var flowDefinition))
        {
            throw new ChordConfigurationException("(flow)", $"Flow '{flowName}' is not registered. Available flows: {string.Join(", ", _flows.Keys)}.");
        }

        if (flowDefinition.Steps.Count == 0)
        {
            throw new ChordConfigurationException("(flow)", $"Flow '{flowName}' does not contain any steps.");
        }

        var correlationId = Guid.NewGuid().ToString("N");
        var flowStartedAt = DateTimeOffset.UtcNow;
        var flowRecord = await _store.CreateFlowInstanceAsync(flowDefinition.Name, correlationId, flowStartedAt, cancellationToken).ConfigureAwait(false);
        var execution = new FlowExecution(flowDefinition, correlationId, flowRecord.Id, flowStartedAt);
        if (!_executions.TryAdd(correlationId, execution))
        {
            throw new InvalidOperationException($"Unable to start flow '{flowName}' because correlation '{correlationId}' already exists.");
        }

        _logger.LogInformation("Starting flow {FlowName} with correlation {CorrelationId}.", flowName, correlationId);

        var payloadBytes = SerializePayload(payload);
        await PublishStepAsync(execution, flowDefinition.Steps[0], payloadBytes, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns the number of actively running flow instances.
    /// </summary>
    public int ActiveFlowCount => _executions.Count;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _listenerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        foreach (var queue in _completionQueues)
        {
            await _messageBus.SubscribeAsync(queue, message => HandleCompletionAsync(message, _listenerCts.Token), _listenerCts.Token).ConfigureAwait(false);
            _logger.LogInformation("Subscribed to completion queue {Queue}.", queue);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _listenerCts?.Cancel();
        return Task.CompletedTask;
    }

    private async Task HandleCompletionAsync(ChordMessage message, CancellationToken cancellationToken)
    {
        if (!message.Headers.TryGetValue(ChordMessageHeaders.CorrelationId, out var correlationId) || string.IsNullOrWhiteSpace(correlationId))
        {
            _logger.LogWarning("Completion message missing correlation id on queue {Queue}.", message.QueueName);
            return;
        }

        if (!_executions.TryGetValue(correlationId, out var execution))
        {
            _logger.LogWarning("Received completion for unknown correlation {CorrelationId}.", correlationId);
            return;
        }

        if (!message.Headers.TryGetValue(ChordMessageHeaders.StepId, out var completedStepId) || string.IsNullOrWhiteSpace(completedStepId))
        {
            _logger.LogWarning("Completion message missing step id for correlation {CorrelationId}.", correlationId);
            return;
        }

        var currentStep = execution.CurrentStep;
        if (!string.Equals(currentStep.Id, completedStepId, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Completion step mismatch. Expected {ExpectedStep} but received {ActualStep} for correlation {CorrelationId}.", currentStep.Id, completedStepId, correlationId);
            return;
        }

        _logger.LogInformation("Flow {CorrelationId} completed step {StepId}.", correlationId, completedStepId);

        await _store.LogMessageAsync(ChordMessageDirection.Inbound, execution.FlowInstanceId, message.QueueName, completedStepId, message.Headers, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
        await CompleteCurrentStepAsync(execution, cancellationToken).ConfigureAwait(false);

        if (!execution.MoveNext())
        {
            _executions.TryRemove(correlationId, out _);
            await CompleteFlowAsync(execution, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Flow {CorrelationId} has completed all steps.", correlationId);
            return;
        }

        var nextStep = execution.CurrentStep;
        await PublishStepAsync(execution, nextStep, message.Body, cancellationToken).ConfigureAwait(false);
    }

    private async Task PublishStepAsync(FlowExecution execution, FlowStep step, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        var stepStartedAt = DateTimeOffset.UtcNow;
        var stepRecord = await _store.CreateStepInstanceAsync(execution.FlowInstanceId, step.Id, stepStartedAt, cancellationToken).ConfigureAwait(false);
        execution.SetCurrentStepInstance(stepRecord.Id, stepStartedAt);

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ChordMessageHeaders.CorrelationId] = execution.CorrelationId,
            [ChordMessageHeaders.StepId] = step.Id
        };

        _logger.LogInformation("Dispatching step {StepId} for flow {FlowName} to queue {Queue}.", step.Id, execution.Definition.Name, step.CommandQueue);

        await _messageBus.PublishAsync(step.CommandQueue, payload, headers, cancellationToken).ConfigureAwait(false);
        await _store.LogMessageAsync(ChordMessageDirection.Outbound, execution.FlowInstanceId, step.CommandQueue, step.Id, headers, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
    }

    private static ReadOnlyMemory<byte> SerializePayload(object payload)
    {
        return JsonSerializer.SerializeToUtf8Bytes(payload, payload.GetType());
    }

    public ValueTask DisposeAsync()
    {
        if (_listenerCts is not null)
        {
            try
            {
                _listenerCts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            _listenerCts.Dispose();
            _listenerCts = null;
        }

        return ValueTask.CompletedTask;
    }

    private async Task CompleteCurrentStepAsync(FlowExecution execution, CancellationToken cancellationToken)
    {
        var stepContext = execution.CurrentStepInstance ?? throw new InvalidOperationException("Active step context is missing.");
        var completedAt = DateTimeOffset.UtcNow;
        var duration = CalculateDuration(stepContext.StartedAtUtc, completedAt);

        await _store.CompleteStepInstanceAsync(stepContext.StepInstanceId, completedAt, duration, cancellationToken).ConfigureAwait(false);
    }

    private async Task CompleteFlowAsync(FlowExecution execution, CancellationToken cancellationToken)
    {
        var completedAt = DateTimeOffset.UtcNow;
        var duration = CalculateDuration(execution.StartedAtUtc, completedAt);
        await _store.CompleteFlowInstanceAsync(execution.FlowInstanceId, completedAt, duration, cancellationToken).ConfigureAwait(false);
    }

    private static long CalculateDuration(DateTimeOffset startedAtUtc, DateTimeOffset completedAtUtc)
    {
        var duration = completedAtUtc - startedAtUtc;
        return duration <= TimeSpan.Zero
            ? 0
            : (long)Math.Round(duration.TotalMilliseconds, MidpointRounding.AwayFromZero);
    }

    private sealed class FlowExecution
    {
        private StepContext? _currentStep;

        public FlowExecution(FlowDefinition definition, string correlationId, Guid flowInstanceId, DateTimeOffset startedAtUtc)
        {
            Definition = definition;
            CorrelationId = correlationId;
            FlowInstanceId = flowInstanceId;
            StartedAtUtc = startedAtUtc;
            CurrentStepIndex = 0;
        }

        public FlowDefinition Definition { get; }

        public string CorrelationId { get; }

        public Guid FlowInstanceId { get; }

        public DateTimeOffset StartedAtUtc { get; }

        public int CurrentStepIndex { get; private set; }

        public FlowStep CurrentStep => Definition.Steps[CurrentStepIndex];

        public bool MoveNext()
        {
            var nextIndex = CurrentStepIndex + 1;
            if (nextIndex >= Definition.Steps.Count)
            {
                _currentStep = null;
                return false;
            }

            CurrentStepIndex = nextIndex;
            _currentStep = null;
            return true;
        }

        public void SetCurrentStepInstance(Guid stepInstanceId, DateTimeOffset startedAtUtc)
        {
            _currentStep = new StepContext(stepInstanceId, startedAtUtc);
        }

        public StepContext? CurrentStepInstance => _currentStep;

        public sealed record StepContext(Guid StepInstanceId, DateTimeOffset StartedAtUtc);
    }
}
