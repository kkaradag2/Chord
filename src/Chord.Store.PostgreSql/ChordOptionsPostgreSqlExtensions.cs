using System;
using Chord;
using Microsoft.Extensions.DependencyInjection;

namespace Chord.Store.PostgreSql;

public static class ChordOptionsPostgreSqlExtensions
{
    private const string ProviderName = "PostgreSqlStore";

    public static ChordOptions UsePostgreSqlStore(this ChordOptions options, Action<PostgreSqlChordStoreOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configure);

        var storeOptions = new PostgreSqlChordStoreOptions();
        configure(storeOptions);
        ValidatePostgreSqlOptions(storeOptions);

        return options.RegisterStoreProvider(ProviderName, services =>
        {
            var snapshot = storeOptions.Clone();
            services.AddSingleton(snapshot);
            services.AddSingleton<IChordStore, PostgreSqlChordStore>();
        });
    }

    private static void ValidatePostgreSqlOptions(PostgreSqlChordStoreOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new ChordConfigurationException("(store)", "PostgreSQL store requires a connection string.");
        }

        if (string.IsNullOrWhiteSpace(options.Host))
        {
            throw new ChordConfigurationException("(store)", "PostgreSQL store requires a host.");
        }

        if (options.Port <= 0)
        {
            throw new ChordConfigurationException("(store)", "PostgreSQL store requires a valid port.");
        }

        if (string.IsNullOrWhiteSpace(options.Database))
        {
            throw new ChordConfigurationException("(store)", "PostgreSQL store requires a database name.");
        }

        if (string.IsNullOrWhiteSpace(options.Username))
        {
            throw new ChordConfigurationException("(store)", "PostgreSQL store requires a username.");
        }
    }
}
