using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Chord.Core.Stores;

namespace Chord.Store.PostgreSql;

/// <summary>
/// Placeholder PostgreSQL store implementation.
/// </summary>
internal sealed class PostgreSqlChordStore : IChordStore, IChordStoreSnapshotProvider
{
    public PostgreSqlStoreOptions Options { get; }

    public PostgreSqlChordStore(PostgreSqlStoreOptions options)
    {
        Options = options;
    }

    public ValueTask RecordDispatchAsync(FlowDispatchRecord record, CancellationToken cancellationToken = default)
    {
        // Implementation placeholder; actual persistence will be added in future work.
        return ValueTask.CompletedTask;
    }

    public ValueTask UpdateDispatchAsync(string correlationId, FlowDispatchStatus status, string payload, CancellationToken cancellationToken = default)
    {
        // Implementation placeholder; actual persistence will be added in future work.
        return ValueTask.CompletedTask;
    }

    public IReadOnlyDictionary<string, IReadOnlyList<FlowDispatchRecord>> GetSnapshot()
    {
        return new Dictionary<string, IReadOnlyList<FlowDispatchRecord>>();
    }
}
