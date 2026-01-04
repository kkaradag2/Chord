namespace Chord.Store.PostgreSql;

/// <summary>
/// Represents PostgreSQL store configuration.
/// </summary>
public sealed class PostgreSqlChordStoreOptions
{
    public string ConnectionString { get; set; } = string.Empty;

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 5432;

    public string Database { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public PostgreSqlChordStoreOptions Clone()
    {
        return new PostgreSqlChordStoreOptions
        {
            ConnectionString = ConnectionString,
            Host = Host,
            Port = Port,
            Database = Database,
            Username = Username,
            Password = Password
        };
    }
}
