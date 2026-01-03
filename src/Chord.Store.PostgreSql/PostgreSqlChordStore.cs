using System.Threading;
using System.Threading.Tasks;
using Chord.Core.Stores;

namespace Chord.Store.PostgreSql;

/// <summary>
/// Placeholder PostgreSQL store implementation.
/// </summary>
internal sealed class PostgreSqlChordStore : IChordStore
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
}
