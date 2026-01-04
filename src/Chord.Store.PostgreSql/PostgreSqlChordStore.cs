using Chord;

namespace Chord.Store.PostgreSql;

/// <summary>
/// Placeholder implementation for the PostgreSQL store until persistence is implemented.
/// </summary>
public sealed class PostgreSqlChordStore : IChordStore
{
    public PostgreSqlChordStore(PostgreSqlChordStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
    }
}
