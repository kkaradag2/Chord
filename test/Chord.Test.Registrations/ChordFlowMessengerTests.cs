using System;
using System.Text;
using System.Threading.Tasks;
using Chord.Core.Exceptions;
using Chord.Core.Flows;
using Chord.Core.Stores;
using Chord.Messaging.RabitMQ.Messaging;

namespace Chord.Test.Registrations;

public class ChordFlowMessengerTests
{
    /// <summary>
    /// Verifies the orchestrator publishes to the queue defined by the second step in the workflow.
    /// </summary>
    [Fact]
    public async Task StartAsync_UsesSecondStepQueue()
    {
        var flow = BuildFlow("host", "payment.command");
        var publisher = new FakePublisher();
        var store = new FakeStore();
        var messenger = new ChordFlowMessenger(new FakeFlowProvider(flow), publisher, store);

        await messenger.StartAsync("payload");

        Assert.Equal("payment.command", publisher.QueueName);
        Assert.Equal("payload", Encoding.UTF8.GetString(publisher.Payload.ToArray()));
        Assert.False(string.IsNullOrWhiteSpace(publisher.CorrelationId));
        Assert.Single(store.Records);
        var record = store.Records[0];
        Assert.Equal("payment", record.StepId);
        Assert.Equal("payment.command", record.QueueName);
        Assert.Equal(FlowDispatchStatus.InProgress, record.Status);
        Assert.Equal("payload", record.Payload);
    }

    /// <summary>
    /// Ensures flows without at least two steps throw before attempting to publish.
    /// </summary>
    [Fact]
    public async Task StartAsync_WithSingleStepFlow_Throws()
    {
        var steps = new[]
        {
            CreateStep("host", "host.queue")
        };

        var flow = new ChordFlowDefinition("Test", "1.0", steps);
        var messenger = new ChordFlowMessenger(new FakeFlowProvider(flow), new FakePublisher(), new FakeStore());

        var exception = await Assert.ThrowsAsync<ChordConfigurationException>(() => messenger.StartAsync("payload").AsTask());
        Assert.Contains("at least two steps", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ChordFlowDefinition BuildFlow(string hostStepQueue, string secondStepQueue)
    {
        var steps = new[]
        {
            CreateStep("host", hostStepQueue),
            CreateStep("payment", secondStepQueue)
        };

        return new ChordFlowDefinition("OrderFlow", "1.0", steps);
    }

    private static ChordFlowStep CreateStep(string id, string queue) =>
        new(
            id,
            $"{id}Service",
            new FlowCommand($"{id}Requested", queue),
            new FlowCompletion($"{id}Completed", "5s", null),
            Array.Empty<FlowRollback>());

    private sealed class FakeFlowProvider : IChordFlowDefinitionProvider
    {
        public FakeFlowProvider(ChordFlowDefinition flow) => Flow = flow;
        public ChordFlowDefinition Flow { get; }
    }

    private sealed class FakePublisher : IChordMessagePublisher
    {
        public string? QueueName { get; private set; }
        public ReadOnlyMemory<byte> Payload { get; private set; }
        public string? CorrelationId { get; private set; }

        public ValueTask PublishAsync(string queueName, ReadOnlyMemory<byte> body, string? correlationId = null, System.Threading.CancellationToken cancellationToken = default)
        {
            QueueName = queueName;
            Payload = body;
            CorrelationId = correlationId;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeStore : IChordStore
    {
        public List<FlowDispatchRecord> Records { get; } = new();

        public ValueTask RecordDispatchAsync(FlowDispatchRecord record, System.Threading.CancellationToken cancellationToken = default)
        {
            Records.Add(record);
            return ValueTask.CompletedTask;
        }
    }
}
