using System.Collections.Generic;

namespace Chord.Core.Stores;

/// <summary>
/// Provides read access to store snapshots for diagnostics and dashboards.
/// </summary>
public interface IChordStoreSnapshotProvider
{
    IReadOnlyDictionary<string, IReadOnlyList<FlowDispatchRecord>> GetSnapshot();
}
