using System.Linq;
using System.Text.Json;
using Chord;
using Chord.Store.InMemory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Chord.Tests.Orchestration;

public class FlowOrchestrationTests
{
    private static string TestDataPath => Path.Combine(AppContext.BaseDirectory, "TestData");

    [Fact]
    public async Task StartFlowAsync_DispatchesFirstStep_And_CompletesAfterAllSteps()
    {
        await using var environment = await FlowTestEnvironment.CreateAsync(Path.Combine(TestDataPath, "order-flow.yaml"));
        var runtime = environment.ServiceProvider.GetRequiredService<ChordFlowRuntime>();
        var bus = environment.ServiceProvider.GetRequiredService<FakeChordMessageBus>();

        await runtime.StartFlowAsync("OrderFlow", new { orderId = 42 });

        var firstDispatch = Assert.Single(bus.PublishedMessages.Where(m => m.QueueName == "queue.reserve"));
        var correlationId = firstDispatch.Headers[ChordMessageHeaders.CorrelationId];
        Assert.Equal("reserve", firstDispatch.Headers[ChordMessageHeaders.StepId]);

        await bus.EmitCompletionAsync("queue.order-completed", correlationId, "reserve", new { orderId = 42 });

        var captureDispatch = Assert.Single(bus.PublishedMessages.Where(m => m.QueueName == "queue.capture"));
        Assert.Equal(correlationId, captureDispatch.Headers[ChordMessageHeaders.CorrelationId]);
        Assert.Equal("capture", captureDispatch.Headers[ChordMessageHeaders.StepId]);

        await bus.EmitCompletionAsync("queue.order-completed", correlationId, "capture", new { orderId = 42 });

        Assert.Equal(0, runtime.ActiveFlowCount);
    }

    [Fact]
    public async Task EcommerceFlow_PersistsFlowAndStepInstances()
    {
        var flowPath = Path.Combine(TestDataPath, "ecommerce-flow.yaml");
        await using var environment = await FlowTestEnvironment.CreateAsync(flowPath);
        var runtime = environment.ServiceProvider.GetRequiredService<ChordFlowRuntime>();
        var bus = environment.ServiceProvider.GetRequiredService<FakeChordMessageBus>();
        var store = environment.ServiceProvider.GetRequiredService<IChordStore>();

        await runtime.StartFlowAsync("EcommerceFlow", new { orderId = 9001, tenantId = "store-42" });

        var firstDispatch = bus.PublishedMessages.First(m => m.QueueName == "queue.validate-payment");
        var correlationId = firstDispatch.Headers[ChordMessageHeaders.CorrelationId];

        await bus.EmitCompletionAsync("queue.ecommerce-completed", correlationId, "validate-payment", new { orderId = 9001 });
        await bus.EmitCompletionAsync("queue.ecommerce-completed", correlationId, "reserve-inventory", new { orderId = 9001 });
        await bus.EmitCompletionAsync("queue.ecommerce-completed", correlationId, "ship-order", new { orderId = 9001 });
        await bus.EmitCompletionAsync("queue.ecommerce-completed", correlationId, "notify-customer", new { orderId = 9001 });

        Assert.Equal(0, runtime.ActiveFlowCount);

        var flows = await store.QueryFlowInstancesAsync();
        var flow = Assert.Single(flows);
        Assert.Equal("EcommerceFlow", flow.FlowName);
        Assert.Equal(correlationId, flow.CorrelationId);
        Assert.Equal(FlowInstanceStatus.Completed, flow.Status);
        Assert.NotNull(flow.CompletedAtUtc);
        Assert.True(flow.DurationMilliseconds is >= 0);

        var steps = await store.QueryStepInstancesAsync(flow.Id);
        Assert.Equal(new[] { "validate-payment", "reserve-inventory", "ship-order", "notify-customer" }, steps.Select(s => s.StepId));

        foreach (var step in steps)
        {
            Assert.Equal(flow.Id, step.FlowInstanceId);
            Assert.Equal(StepInstanceStatus.Completed, step.Status);
            Assert.NotNull(step.CompletedAtUtc);
            Assert.True(step.DurationMilliseconds is >= 0);
        }
    }

    [Fact]
    public async Task MultipleFlows_RunIndependently()
    {
        await using var environment = await FlowTestEnvironment.CreateAsync(
            Path.Combine(TestDataPath, "order-flow.yaml"),
            Path.Combine(TestDataPath, "shipment-flow.yaml"));

        var runtime = environment.ServiceProvider.GetRequiredService<ChordFlowRuntime>();
        var bus = environment.ServiceProvider.GetRequiredService<FakeChordMessageBus>();

        await runtime.StartFlowAsync("OrderFlow", new { orderId = 1 });
        await runtime.StartFlowAsync("ShipmentFlow", new { shipmentId = 2 });

        var orderCorrelation = bus.PublishedMessages.First(m => m.QueueName == "queue.reserve").Headers[ChordMessageHeaders.CorrelationId];
        var shipmentCorrelation = bus.PublishedMessages.First(m => m.QueueName == "queue.pack").Headers[ChordMessageHeaders.CorrelationId];

        await bus.EmitCompletionAsync("queue.order-completed", orderCorrelation, "reserve", new { orderId = 1 });
        await bus.EmitCompletionAsync("queue.shipment-completed", shipmentCorrelation, "pack", new { shipmentId = 2 });

        var orderNext = bus.PublishedMessages.First(m => m.QueueName == "queue.capture");
        Assert.Equal(orderCorrelation, orderNext.Headers[ChordMessageHeaders.CorrelationId]);

        await bus.EmitCompletionAsync("queue.shipment-completed", shipmentCorrelation, "ship", new { shipmentId = 2 });
        Assert.Equal(2, runtime.ActiveFlowCount); // Order still running (capture pending), shipment waiting final step.

        await bus.EmitCompletionAsync("queue.order-completed", orderCorrelation, "capture", new { orderId = 1 });
        await bus.EmitCompletionAsync("queue.shipment-completed", shipmentCorrelation, "deliver", new { shipmentId = 2 });

        Assert.Equal(0, runtime.ActiveFlowCount);
    }

    private sealed class FlowTestEnvironment : IAsyncDisposable
    {
        public required ServiceProvider ServiceProvider { get; init; }
        private IReadOnlyCollection<IHostedService>? HostedServices { get; init; }

        public static async Task<FlowTestEnvironment> CreateAsync(params string[] flowPaths)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddChord(options =>
            {
                options.UseYamlFlows(flowPaths);
                options.RegisterMessagingProvider("Fake", svc =>
                {
                    svc.AddSingleton<FakeChordMessageBus>();
                    svc.AddSingleton<IChordMessageBus>(sp => sp.GetRequiredService<FakeChordMessageBus>());
                });
                options.UseInMemoryStore();
            });

            var provider = services.BuildServiceProvider();
            var hostedServices = provider.GetServices<IHostedService>().ToArray();
            foreach (var hosted in hostedServices)
            {
                await hosted.StartAsync(CancellationToken.None);
            }

            return new FlowTestEnvironment
            {
                ServiceProvider = provider,
                HostedServices = hostedServices
            };
        }

        public async ValueTask DisposeAsync()
        {
            if (HostedServices is { Count: > 0 })
            {
                foreach (var hosted in HostedServices)
                {
                    await hosted.StopAsync(CancellationToken.None);
                }
            }

            if (ServiceProvider is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else
            {
                ServiceProvider.Dispose();
            }
        }
    }
}
