using Chord.Core.Exceptions;

namespace Chord.Store.PostgreSql;

/// <summary>
/// Options required to set up the PostgreSQL store provider.
/// </summary>
public sealed class PostgreSqlStoreOptions
{
    /// <summary>
    /// Gets or sets the database connection string.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the schema name (optional). Defaults to <c>public</c>.
    /// </summary>
    public string Schema { get; set; } = "public";

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw new ChordConfigurationException("PostgreSQL store requires a ConnectionString.");
        }

        if (string.IsNullOrWhiteSpace(Schema))
        {
            throw new ChordConfigurationException("PostgreSQL store schema cannot be empty.");
        }
    }
}
