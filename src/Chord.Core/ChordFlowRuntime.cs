using System.Collections.Concurrent;

namespace Chord;

/// <summary>
/// Provides the runtime entry point for starting flows from the host application.
/// </summary>
public sealed class ChordFlowRuntime
{
    private readonly ConcurrentDictionary<string, FlowDefinition> _flows;

    public ChordFlowRuntime(IEnumerable<FlowDefinition> flowDefinitions)
    {
        if (flowDefinitions is null)
        {
            throw new ArgumentNullException(nameof(flowDefinitions));
        }

        _flows = new ConcurrentDictionary<string, FlowDefinition>(
            flowDefinitions.ToDictionary(flow => flow.Name, flow => flow, StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Validates and initiates a flow using the provided payload.
    /// </summary>
    /// <param name="flowName">Name of the flow as declared inside the YAML definition.</param>
    /// <param name="payload">Payload to send to the first step of the flow.</param>
    public Task StartFlowAsync(string flowName, object payload, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(flowName))
        {
            throw new ArgumentException("Flow name cannot be null or whitespace.", nameof(flowName));
        }

        ArgumentNullException.ThrowIfNull(payload);

        if (!_flows.TryGetValue(flowName, out var definition))
        {
            throw new ChordConfigurationException("(flow)", $"Flow '{flowName}' is not registered. Available flows: {string.Join(", ", _flows.Keys)}.");
        }

        // Runtime orchestration will be implemented in future iterations. For now, we only validate the request.
        return Task.CompletedTask;
    }
}
