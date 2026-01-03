using System.Text;
using System.Text.Json;
using System.Threading;
using Chord.Core.Flows;
using Chord.Core.Stores;
using Chord.Messaging.RabitMQ.Messaging;
using Chord.Messaging.RabitMQ.Messaging.Completion;

namespace Chord.Test.Registrations;

public class FlowCompletionProcessorTests
{
    [Fact]
    public async Task ProcessAsync_Success_DispatchesNextStep()
    {
        var flow = CreateFlow();
        var store = new FakeStore();
        var publisher = new FakePublisher();
        var processor = new FlowCompletionProcessor(new FakeFlowProvider(flow), publisher, store);

        var message = new FlowCompletionMessage("corr", "order", FlowCompletionStatus.Success, """{"orderId":"1"}""");

        await processor.ProcessAsync(message);

        Assert.Equal(FlowDispatchStatus.Completed, store.UpdatedStatus);
        Assert.Equal("payment.command", publisher.QueueName);
        Assert.NotNull(store.LastDispatchRecord);
        Assert.Equal("payment", store.LastDispatchRecord!.StepId);
    }

    [Fact]
    public async Task ProcessAsync_Failure_DispatchesRollbacks()
    {
        var flow = CreateFlow();
        var store = new FakeStore();
        var publisher = new FakePublisher();
        var processor = new FlowCompletionProcessor(new FakeFlowProvider(flow), publisher, store);

        var payload = """{"orderId":"1"}""";
        var message = new FlowCompletionMessage("corr", "payment", FlowCompletionStatus.Failure, payload);

        await processor.ProcessAsync(message);

        Assert.Equal(FlowDispatchStatus.Failed, store.UpdatedStatus);
        Assert.Equal(payload, publisher.Payload);
        Assert.Equal("orders.command", publisher.QueueName);
    }

    private static ChordFlowDefinition CreateFlow()
    {
        var steps = new[]
        {
            new ChordFlowStep(
                "order",
                "Orders",
                new FlowCommand("OrderRequested", "orders.command"),
                new FlowCompletion("OrderCompleted", "5s", null),
                Array.Empty<FlowRollback>()),
            new ChordFlowStep(
                "payment",
                "Payments",
                new FlowCommand("PaymentRequested", "payment.command"),
                new FlowCompletion("PaymentCompleted", "5s", null),
                new[]
                {
                    new FlowRollback("order", new FlowCommand("OrderRollbackRequested", "orders.command"))
                })
        };

        return new ChordFlowDefinition("Test", "1.0", steps);
    }

    private sealed class FakeFlowProvider : IChordFlowDefinitionProvider
    {
        public FakeFlowProvider(ChordFlowDefinition flow) => Flow = flow;
        public ChordFlowDefinition Flow { get; }
    }

    private sealed class FakeStore : IChordStore
    {
        public FlowDispatchRecord? LastDispatchRecord { get; private set; }
        public FlowDispatchStatus? UpdatedStatus { get; private set; }

        public ValueTask RecordDispatchAsync(FlowDispatchRecord record, CancellationToken cancellationToken = default)
        {
            LastDispatchRecord = record;
            return ValueTask.CompletedTask;
        }

        public ValueTask UpdateDispatchAsync(string correlationId, FlowDispatchStatus status, string payload, CancellationToken cancellationToken = default)
        {
            UpdatedStatus = status;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakePublisher : IChordMessagePublisher
    {
        public string? QueueName { get; private set; }
        public string? Payload { get; private set; }

        public ValueTask PublishAsync(string queueName, ReadOnlyMemory<byte> body, string? correlationId = null, CancellationToken cancellationToken = default)
        {
            QueueName = queueName;
            Payload = Encoding.UTF8.GetString(body.Span);
            return ValueTask.CompletedTask;
        }
    }
}
