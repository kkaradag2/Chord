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
    private readonly ILogger<ChordFlowRuntime> _logger;
    private readonly ConcurrentDictionary<string, FlowDefinition> _flows;
    private readonly ConcurrentDictionary<string, FlowExecution> _executions = new();
    private readonly HashSet<string> _completionQueues;
    private CancellationTokenSource? _listenerCts;

    public ChordFlowRuntime(ChordOptions options, IChordMessageBus messageBus, ILogger<ChordFlowRuntime> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
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
        var execution = new FlowExecution(flowDefinition, correlationId);
        if (!_executions.TryAdd(correlationId, execution))
        {
            throw new InvalidOperationException($"Unable to start flow '{flowName}' because correlation '{correlationId}' already exists.");
        }

        _logger.LogInformation("Starting flow {FlowName} with correlation {CorrelationId}.", flowName, correlationId);

        var payloadBytes = SerializePayload(payload);
        await PublishStepAsync(flowDefinition, flowDefinition.Steps[0], correlationId, payloadBytes, cancellationToken);
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

        if (!execution.MoveNext())
        {
            _executions.TryRemove(correlationId, out _);
            _logger.LogInformation("Flow {CorrelationId} has completed all steps.", correlationId);
            return;
        }

        var nextStep = execution.CurrentStep;
        await PublishStepAsync(execution.Definition, nextStep, correlationId, message.Body, cancellationToken).ConfigureAwait(false);
    }

    private Task PublishStepAsync(FlowDefinition definition, FlowStep step, string correlationId, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ChordMessageHeaders.CorrelationId] = correlationId,
            [ChordMessageHeaders.StepId] = step.Id
        };

        _logger.LogInformation("Dispatching step {StepId} for flow {FlowName} to queue {Queue}.", step.Id, definition.Name, step.CommandQueue);

        return _messageBus.PublishAsync(step.CommandQueue, payload, headers, cancellationToken);
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

    private sealed class FlowExecution
    {
        public FlowExecution(FlowDefinition definition, string correlationId)
        {
            Definition = definition;
            CorrelationId = correlationId;
            CurrentStepIndex = 0;
        }

        public FlowDefinition Definition { get; }

        public string CorrelationId { get; }

        public int CurrentStepIndex { get; private set; }

        public FlowStep CurrentStep => Definition.Steps[CurrentStepIndex];

        public bool MoveNext()
        {
            var nextIndex = CurrentStepIndex + 1;
            if (nextIndex >= Definition.Steps.Count)
            {
                return false;
            }

            CurrentStepIndex = nextIndex;
            return true;
        }
    }
}
