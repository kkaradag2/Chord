using System.IO;
using System.Linq;
using System.Reflection;
using Npgsql;

namespace Chord.Store.PostgreSql;

internal sealed class PostgreSqlSchemaMigrator
{
    private readonly PostgreSqlChordStoreOptions _options;

    public PostgreSqlSchemaMigrator(PostgreSqlChordStoreOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public void EnsureSchema()
    {
        using var connection = new NpgsqlConnection(_options.ConnectionString);
        connection.Open();

        foreach (var script in LoadScripts())
        {
            using var command = new NpgsqlCommand(script, connection);
            command.ExecuteNonQuery();
        }
    }

    private static IEnumerable<string> LoadScripts()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames()
            .Where(resource => resource.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .OrderBy(resource => resource, StringComparer.OrdinalIgnoreCase);

        foreach (var resourceName in resourceNames)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Unable to load resource '{resourceName}'.");
            using var reader = new StreamReader(stream);
            yield return reader.ReadToEnd();
        }
    }
}
