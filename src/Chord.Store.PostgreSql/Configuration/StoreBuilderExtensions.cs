using System;
using Chord.Core.Configuration;
using Chord.Core.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace Chord.Store.PostgreSql.Configuration;

/// <summary>
/// Adds PostgreSQL store registration helpers.
/// </summary>
public static class StoreBuilderExtensions
{
    public static void PostgreSql(this StoreBuilder builder, Action<PostgreSqlStoreOptions> configure)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        var options = new PostgreSqlStoreOptions();
        configure(options);
        options.Validate();

        builder.UseProvider("PostgreSql", services =>
        {
            services.AddSingleton(options);
            services.AddSingleton<IChordStore, PostgreSqlChordStore>();
        });
    }
}
