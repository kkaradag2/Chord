using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Chord.Core.Flows;
using Chord.Core.Stores;

namespace Chord.Messaging.RabitMQ.Messaging.Completion;

internal interface IFlowCompletionProcessor
{
    Task ProcessAsync(FlowCompletionMessage message, CancellationToken cancellationToken = default);
}

internal sealed class FlowCompletionProcessor : IFlowCompletionProcessor
{
    private readonly IChordFlowDefinitionProvider _flowProvider;
    private readonly IChordMessagePublisher _publisher;
    private readonly IChordStore _store;

    public FlowCompletionProcessor(IChordFlowDefinitionProvider flowProvider, IChordMessagePublisher publisher, IChordStore store)
    {
        _flowProvider = flowProvider ?? throw new ArgumentNullException(nameof(flowProvider));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task ProcessAsync(FlowCompletionMessage message, CancellationToken cancellationToken = default)
    {
        var flow = _flowProvider.Flow;
        var currentStepIndex = flow.Steps.Select((step, index) => (step, index))
            .Where(tuple => string.Equals(tuple.step.Id, message.StepId, StringComparison.OrdinalIgnoreCase))
            .Select(tuple => tuple.index)
            .FirstOrDefault(-1);

        if (currentStepIndex < 0)
        {
            throw new InvalidOperationException($"Unknown step '{message.StepId}' reported in completion message.");
        }

        var status = message.Status == FlowCompletionStatus.Success
            ? FlowDispatchStatus.Completed
            : FlowDispatchStatus.Failed;

        await _store.UpdateDispatchAsync(message.CorrelationId, status, message.Payload, cancellationToken).ConfigureAwait(false);

        if (message.Status == FlowCompletionStatus.Success)
        {
            await DispatchNextStepAsync(flow, currentStepIndex, message, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await DispatchRollbacksAsync(flow.Steps[currentStepIndex], message, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task DispatchNextStepAsync(ChordFlowDefinition flow, int currentIndex, FlowCompletionMessage message, CancellationToken cancellationToken)
    {
        if (currentIndex + 1 >= flow.Steps.Count)
        {
            return;
        }

        var next = flow.Steps[currentIndex + 1];
        var record = new FlowDispatchRecord(
            message.CorrelationId,
            next.Id,
            next.Command.Queue,
            FlowDispatchStatus.InProgress,
            DateTimeOffset.UtcNow,
            null,
            TimeSpan.Zero,
            message.Payload,
            null);

        await _store.RecordDispatchAsync(record, cancellationToken).ConfigureAwait(false);
        await PublishAsync(next.Command.Queue, message.Payload, message.CorrelationId, cancellationToken).ConfigureAwait(false);
    }

    private async Task DispatchRollbacksAsync(ChordFlowStep currentStep, FlowCompletionMessage message, CancellationToken cancellationToken)
    {
        if (currentStep.Rollbacks.Count == 0)
        {
            return;
        }

        foreach (var rollback in currentStep.Rollbacks)
        {
            await PublishAsync(rollback.Command.Queue, message.Payload, message.CorrelationId, cancellationToken).ConfigureAwait(false);
        }
    }

    private Task PublishAsync(string queueName, string payload, string correlationId, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(payload);
        return _publisher.PublishAsync(queueName, bytes, correlationId, cancellationToken).AsTask();
    }
}
