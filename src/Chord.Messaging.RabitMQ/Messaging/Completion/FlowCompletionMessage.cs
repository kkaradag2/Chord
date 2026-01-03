using System;
using System.Text.Json;

namespace Chord.Messaging.RabitMQ.Messaging.Completion;

internal sealed record FlowCompletionMessage(
    string CorrelationId,
    string StepId,
    FlowCompletionStatus Status,
    string Payload)
{
    public static FlowCompletionMessage Parse(string correlationId, string rawJson)
    {
        using var document = JsonDocument.Parse(rawJson);
        var root = document.RootElement;

        var stepId = root.TryGetProperty("stepId", out var stepProperty)
            ? stepProperty.GetString()
            : null;

        var statusText = root.TryGetProperty("status", out var statusProperty)
            ? statusProperty.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(stepId))
        {
            throw new InvalidOperationException("Completion message is missing stepId.");
        }

        if (!Enum.TryParse(statusText, true, out FlowCompletionStatus status))
        {
            throw new InvalidOperationException($"Completion message contains unknown status '{statusText}'.");
        }

        var payloadElement = root.TryGetProperty("payload", out var payloadProperty)
            ? payloadProperty
            : default;

        var payload = payloadElement.ValueKind == JsonValueKind.Undefined ? "{}" : payloadElement.GetRawText();

        return new FlowCompletionMessage(
            correlationId,
            stepId!,
            status,
            payload);
    }
}

internal enum FlowCompletionStatus
{
    Success,
    Failure
}
