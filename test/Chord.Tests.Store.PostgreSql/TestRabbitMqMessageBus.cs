using System.Collections.Concurrent;
using System.Text.Json;
using Chord;

namespace Chord.Tests.Store.PostgreSql;

internal sealed class TestRabbitMqMessageBus : IChordMessageBus
{
    private readonly ConcurrentDictionary<string, Func<ChordMessage, Task>> _subscriptions = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<PublishedMessage> _published = new();

    public IReadOnlyCollection<PublishedMessage> PublishedMessages
    {
        get
        {
            lock (_published)
            {
                return _published.ToArray();
            }
        }
    }

    public Task PublishAsync(string queueName, ReadOnlyMemory<byte> payload, IReadOnlyDictionary<string, string>? headers = null, CancellationToken cancellationToken = default)
    {
        var message = new PublishedMessage(queueName, payload.ToArray(), headers ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        lock (_published)
        {
            _published.Add(message);
        }

        return Task.CompletedTask;
    }

    public Task SubscribeAsync(string queueName, Func<ChordMessage, Task> handler, CancellationToken cancellationToken)
    {
        if (!_subscriptions.TryAdd(queueName, handler))
        {
            _subscriptions[queueName] = handler;
        }

        cancellationToken.Register(() => _subscriptions.TryRemove(queueName, out _));
        return Task.CompletedTask;
    }

    public async Task EmitCompletionAsync(string queueName, string correlationId, string stepId, object payload)
    {
        if (!_subscriptions.TryGetValue(queueName, out var handler))
        {
            throw new InvalidOperationException($"No handler registered for queue '{queueName}'.");
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ChordMessageHeaders.CorrelationId] = correlationId,
            [ChordMessageHeaders.StepId] = stepId
        };

        var body = JsonSerializer.SerializeToUtf8Bytes(payload, payload.GetType());
        await handler(new ChordMessage(queueName, body, headers)).ConfigureAwait(false);
    }

    public sealed record PublishedMessage(string QueueName, byte[] Payload, IReadOnlyDictionary<string, string> Headers);
}
