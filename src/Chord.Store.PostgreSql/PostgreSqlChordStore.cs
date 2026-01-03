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
}
