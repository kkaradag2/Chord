using System;
using System.IO;
using Chord.Core;
using Chord.Core.Exceptions;
using Chord.Core.Stores;
using Chord.Store.InMemory.Configuration;
using Chord.Store.PostgreSql;
using Chord.Store.PostgreSql.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chord.Test.Registrations;

public class StoreConfigurationTests
{
    private static string SampleFlow => Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SamplesYamls", "order-flow.yaml");

    [Fact]
    public void AddChord_WithoutStore_Throws()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<ChordConfigurationException>(() =>
        {
            services.AddChord(config =>
            {
                config.Flow(flow => flow.FromYamlFile(SampleFlow));
                // Missing store configuration
            });
        });

        Assert.Contains("store", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Store_InMemory_RegistersChordStore()
    {
        var services = new ServiceCollection();
        services.AddChord(config =>
        {
            config.Flow(flow => flow.FromYamlFile(SampleFlow));
            config.Store(store => store.InMemory());
        });

        using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IChordStore>();

        Assert.NotNull(store);
        Assert.Equal("Chord.Store.InMemory.InMemoryChordStore", store.GetType().FullName);
    }

    [Fact]
    public void Store_PostgreSql_RequiresConnectionString()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<ChordConfigurationException>(() =>
        {
            services.AddChord(config =>
            {
                config.Flow(flow => flow.FromYamlFile(SampleFlow));
                config.Store(store => store.PostgreSql(options =>
                {
                    options.Schema = "orchestration";
                }));
            });
        });

        Assert.Contains("ConnectionString", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Store_PostgreSql_RegistersOptionsAndProvider()
    {
        var services = new ServiceCollection();

        services.AddChord(config =>
        {
            config.Flow(flow => flow.FromYamlFile(SampleFlow));
            config.Store(store => store.PostgreSql(options =>
            {
                options.ConnectionString = "Host=localhost;Database=chord;Username=postgres;Password=postgres;";
                options.Schema = "orchestration";
            }));
        });

        using var provider = services.BuildServiceProvider();

        var store = provider.GetRequiredService<IChordStore>();
        var options = provider.GetRequiredService<PostgreSqlStoreOptions>();

        Assert.Equal("orchestration", options.Schema);
        Assert.Equal("Chord.Store.PostgreSql.PostgreSqlChordStore", store.GetType().FullName);
    }

    [Fact]
    public void Store_DoubleRegistration_Throws()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<ChordConfigurationException>(() =>
        {
            services.AddChord(config =>
            {
                config.Flow(flow => flow.FromYamlFile(SampleFlow));
                config.Store(store =>
                {
                    store.InMemory();
                    store.PostgreSql(options =>
                    {
                        options.ConnectionString = "Host=localhost;Database=chord;Username=postgres;Password=postgres;";
                    });
                });
            });
        });

        Assert.Contains("Only one store provider", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
