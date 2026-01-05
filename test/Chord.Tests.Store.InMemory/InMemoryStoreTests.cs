using System.Linq;
using Chord;
using Chord.Store.InMemory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Chord.Tests.Store.InMemory;

public class InMemoryStoreTests
{
    private static string FlowPath => Path.Combine(AppContext.BaseDirectory, "TestData", "inmemory-flow.yaml");

    [Fact]
    public async Task UseInMemoryStore_PersistsFlowStepAndMessageLogs()
    {
        await using var environment = await InMemoryFlowEnvironment.CreateAsync(FlowPath);
        var runtime = environment.ServiceProvider.GetRequiredService<ChordFlowRuntime>();
        var bus = environment.ServiceProvider.GetRequiredService<TestInMemoryMessageBus>();
        var store = environment.ServiceProvider.GetRequiredService<IChordStore>();

        Assert.IsType<InMemoryChordStore>(store);

        await runtime.StartFlowAsync("InMemoryFlow", new { orderId = 101 });
        var correlationId = bus.PublishedMessages.First().Headers[ChordMessageHeaders.CorrelationId];

        await bus.EmitCompletionAsync("queue.memory-completed", correlationId, "validate", new { orderId = 101 });
        await bus.EmitCompletionAsync("queue.memory-completed", correlationId, "charge", new { orderId = 101 });
        await bus.EmitCompletionAsync("queue.memory-completed", correlationId, "notify", new { orderId = 101 });

        Assert.Equal(0, runtime.ActiveFlowCount);

        var flows = await store.QueryFlowInstancesAsync();
        var flow = Assert.Single(flows);
        Assert.Equal("InMemoryFlow", flow.FlowName);
        Assert.Equal(correlationId, flow.CorrelationId);
        Assert.Equal(FlowInstanceStatus.Completed, flow.Status);
        Assert.NotNull(flow.CompletedAtUtc);
        Assert.True(flow.DurationMilliseconds is >= 0);

        var steps = await store.QueryStepInstancesAsync(flow.Id);
        Assert.Equal(new[] { "validate", "charge", "notify" }, steps.Select(s => s.StepId));
        Assert.All(steps, step =>
        {
            Assert.Equal(flow.Id, step.FlowInstanceId);
            Assert.Equal(StepInstanceStatus.Completed, step.Status);
            Assert.NotNull(step.CompletedAtUtc);
            Assert.True(step.DurationMilliseconds is >= 0);
        });

        var logs = await store.QueryMessageLogsAsync(flow.Id);
        Assert.Equal(6, logs.Count);

        Assert.Equal(
            new[]
            {
                (ChordMessageDirection.Outbound, "queue.validate"),
                (ChordMessageDirection.Inbound, "queue.memory-completed"),
                (ChordMessageDirection.Outbound, "queue.charge"),
                (ChordMessageDirection.Inbound, "queue.memory-completed"),
                (ChordMessageDirection.Outbound, "queue.notify"),
                (ChordMessageDirection.Inbound, "queue.memory-completed")
            },
            logs.Select(log => (log.Direction, log.QueueName)).ToArray());
    }

    private sealed class InMemoryFlowEnvironment : IAsyncDisposable
    {
        public required ServiceProvider ServiceProvider { get; init; }
        private IReadOnlyCollection<IHostedService>? HostedServices { get; init; }

        public static async Task<InMemoryFlowEnvironment> CreateAsync(string flowPath)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddChord(options =>
            {
                options.UseYamlFlows(flowPath);
                options.RegisterMessagingProvider("TestBus", svc =>
                {
                    svc.AddSingleton<TestInMemoryMessageBus>();
                    svc.AddSingleton<IChordMessageBus>(sp => sp.GetRequiredService<TestInMemoryMessageBus>());
                });
                options.UseInMemoryStore();
            });

            var provider = services.BuildServiceProvider();
            var hosted = provider.GetServices<IHostedService>().ToArray();
            foreach (var service in hosted)
            {
                await service.StartAsync(CancellationToken.None);
            }

            return new InMemoryFlowEnvironment
            {
                ServiceProvider = provider,
                HostedServices = hosted
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
