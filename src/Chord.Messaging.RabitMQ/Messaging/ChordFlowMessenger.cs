using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Chord.Core.Exceptions;
using Chord.Core.Flows;

namespace Chord.Messaging.RabitMQ.Messaging;

/// <summary>
/// Implements the workflow trigger logic by using the registered messaging provider to publish to the next queue.
/// </summary>
internal sealed class ChordFlowMessenger : IChordFlowMessenger
{
    private readonly IChordFlowDefinitionProvider _flowProvider;
    private readonly IChordMessagePublisher _publisher;

    public ChordFlowMessenger(IChordFlowDefinitionProvider flowProvider, IChordMessagePublisher publisher)
    {
        _flowProvider = flowProvider ?? throw new ArgumentNullException(nameof(flowProvider));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
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
        return _publisher.PublishAsync(nextStep.Command.Queue, payload, correlationId, cancellationToken);
    }

    public ValueTask StartAsync(string payload, CancellationToken cancellationToken = default)
    {
        var data = Encoding.UTF8.GetBytes(payload ?? string.Empty);
        return StartAsync(data, cancellationToken);
    }

    private static string CreateCorrelationId() => Guid.NewGuid().ToString("N");
}
