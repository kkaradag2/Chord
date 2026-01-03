using System;
using System.Threading;
using System.Threading.Tasks;

namespace Chord.Messaging.RabitMQ.Messaging;

/// <summary>
/// Exposes the host entry point that kicks off a Chord workflow by delivering the initial message to the next queue.
/// </summary>
public interface IChordFlowMessenger
{
    /// <summary>
    /// Starts the workflow using binary payloads.
    /// </summary>
    ValueTask StartAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the workflow using UTF-8 encoded text payloads.
    /// </summary>
    ValueTask StartAsync(string payload, CancellationToken cancellationToken = default);
}
