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
}
