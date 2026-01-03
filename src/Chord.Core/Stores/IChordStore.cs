using System.Threading;
using System.Threading.Tasks;

namespace Chord.Core.Stores;

/// <summary>
/// Represents the persistence component Chord uses to record flow progress.
/// </summary>
public interface IChordStore
{
    /// <summary>
    /// Records the dispatch of a workflow step.
    /// </summary>
    ValueTask RecordDispatchAsync(FlowDispatchRecord record, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing dispatch with the result that arrived on a queue.
    /// </summary>
    ValueTask UpdateDispatchAsync(string correlationId, FlowDispatchStatus status, string payload, CancellationToken cancellationToken = default);
}
