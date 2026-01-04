using System.Text.Json;
using Chord;
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
