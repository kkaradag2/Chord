using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Chord.Core.Stores;

namespace Chord.Store.InMemory;

/// <summary>
/// In-memory implementation that keeps dispatch history for diagnostics/testing.
/// </summary>
internal sealed class InMemoryChordStore : IChordStore
{
    private readonly ConcurrentDictionary<string, List<FlowDispatchRecord>> _records = new(StringComparer.OrdinalIgnoreCase);

    public ValueTask RecordDispatchAsync(FlowDispatchRecord record, CancellationToken cancellationToken = default)
    {
        var list = _records.GetOrAdd(record.CorrelationId, _ => new List<FlowDispatchRecord>());
        lock (list)
        {
            list.Add(record);
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Exposes the recorded dispatches for test verification.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<FlowDispatchRecord>> Records
    {
        get
        {
            var snapshot = new Dictionary<string, IReadOnlyList<FlowDispatchRecord>>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in _records)
            {
                lock (pair.Value)
                {
                    snapshot[pair.Key] = pair.Value.ToArray();
                }
            }

            return snapshot;
        }
    }
}
