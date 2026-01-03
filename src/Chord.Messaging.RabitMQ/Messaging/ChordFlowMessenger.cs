using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Chord.Core.Exceptions;
using Chord.Core.Flows;
using Chord.Core.Stores;

namespace Chord.Messaging.RabitMQ.Messaging;

/// <summary>
/// Implements the workflow trigger logic by using the registered messaging provider to publish to the next queue.
/// </summary>
internal sealed class ChordFlowMessenger : IChordFlowMessenger
{
    private readonly IChordFlowDefinitionProvider _flowProvider;
    private readonly IChordMessagePublisher _publisher;
    private readonly IChordStore _store;

    public ChordFlowMessenger(IChordFlowDefinitionProvider flowProvider, IChordMessagePublisher publisher, IChordStore store)
    {
        _flowProvider = flowProvider ?? throw new ArgumentNullException(nameof(flowProvider));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public ValueTask StartAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        var flow = _flowProvider.Flow;
        if (flow.Steps.Count < 2)
        {
            throw new ChordConfigurationException("Workflow must declare at least two steps to dispatch the initial message.");
        }

        var nextStep = flow.Steps[1];
        var correlationId = CreateCorrelationId();
        var payloadText = ConvertPayloadToText(payload);

        var record = new FlowDispatchRecord(
            correlationId,
            nextStep.Id,
            nextStep.Command.Queue,
            FlowDispatchStatus.InProgress,
            DateTimeOffset.UtcNow,
            null,
            TimeSpan.Zero,
            payloadText);

        return DispatchAsync(nextStep.Command.Queue, payload, correlationId, record, cancellationToken);
    }

    public ValueTask StartAsync(string payload, CancellationToken cancellationToken = default)
    {
        var data = Encoding.UTF8.GetBytes(payload ?? string.Empty);
        return StartAsync(data, cancellationToken);
    }

    private async ValueTask DispatchAsync(string queue, ReadOnlyMemory<byte> payload, string correlationId, FlowDispatchRecord record, CancellationToken cancellationToken)
    {
        await _store.RecordDispatchAsync(record, cancellationToken).ConfigureAwait(false);
        await _publisher.PublishAsync(queue, payload, correlationId, cancellationToken).ConfigureAwait(false);
    }

    private static string CreateCorrelationId() => Guid.NewGuid().ToString("N");

    private static string ConvertPayloadToText(ReadOnlyMemory<byte> payload)
    {
        if (payload.IsEmpty)
        {
            return string.Empty;
        }

        try
        {
            return Encoding.UTF8.GetString(payload.Span);
        }
        catch
        {
            return Convert.ToBase64String(payload.Span);
        }
    }
}
