using Chord;
using Chord.Messaging.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Chord.Tests.Registration;

public class ChordFlowRuntimeTests
{
    private static string TestDataPath => Path.Combine(AppContext.BaseDirectory, "TestData");

    [Fact]
    public async Task StartFlowAsync_Succeeds_For_Registered_Flow()
    {
        var flowPath = Path.Combine(TestDataPath, "valid-flow.yaml");
        var runtime = BuildRuntime(flowPath);

        await runtime.StartFlowAsync("ValidFlow", new { OrderId = 1 });
    }

    [Fact]
    public async Task StartFlowAsync_Fails_For_Unknown_Flow()
    {
        var flowPath = Path.Combine(TestDataPath, "valid-flow.yaml");
        var runtime = BuildRuntime(flowPath);

        var ex = await Assert.ThrowsAsync<ChordConfigurationException>(() => runtime.StartFlowAsync("MissingFlow", new { OrderId = 1 }));

        Assert.Equal("Chord configuration error for '(flow)': Flow 'MissingFlow' is not registered. Available flows: ValidFlow.", ex.Message);
    }

    private static ChordFlowRuntime BuildRuntime(params string[] flowPaths)
    {
        var services = new ServiceCollection();
        services.AddChord(options =>
        {
            options.UseYamlFlows(flowPaths);
            // UseRabbitMq requires a live broker; for tests we rely on Kafka registration stub.
            options.UseKafka(kafka =>
            {
                kafka.BootstrapServers = "localhost:9092";
                kafka.DefaultTopic = "orders";
            });
        });

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<ChordFlowRuntime>();
    }
}
