using Chord;
using Chord.Messaging.Kafka;
using Chord.Store.PostgreSql;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Chord.Tests.Registration;

public class StoreProviderConfigurationTests
{
    [Fact]
    public void AddChord_WithInMemoryStore_Succeeds()
    {
        var services = CreateServices();

        services.AddChord(options =>
        {
            options.UseKafka(ConfigKafka);
            options.UseInMemoryStore();
        });

        using var provider = services.BuildServiceProvider();

        var store = provider.GetRequiredService<IChordStore>();
        Assert.IsType<InMemoryChordStore>(store);
    }

    [Fact]
    public void AddChord_WithPostgreSqlStore_Succeeds()
    {
        var services = CreateServices();

        services.AddChord(options =>
        {
            options.UseKafka(ConfigKafka);
            options.UsePostgreSqlStore(store =>
            {
                store.ConnectionString = "Host=localhost;Database=chord;Username=postgres;Password=secret;";
                store.Host = "localhost";
                store.Port = 5432;
                store.Database = "chord";
                store.Username = "postgres";
                store.Password = "secret";
            });
        });

        using var provider = services.BuildServiceProvider();

        var store = provider.GetRequiredService<IChordStore>();
        Assert.IsType<PostgreSqlChordStore>(store);
    }

    [Fact]
    public void AddChord_WithMultipleStores_Throws()
    {
        var services = CreateServices();

        var ex = Assert.Throws<ChordConfigurationException>(() =>
        {
            services.AddChord(options =>
            {
                options.UseKafka(ConfigKafka);
                options.UseInMemoryStore();
                options.UsePostgreSqlStore(ValidPostgresOptions);
            });
        });

        Assert.Equal("Chord configuration error for '(store)': Exactly one store provider must be configured, but 2 were provided (InMemoryStore, PostgreSqlStore).", ex.Message);
    }

    [Fact]
    public void AddChord_WithoutStoreProvider_Throws()
    {
        var services = CreateServices();

        var ex = Assert.Throws<ChordConfigurationException>(() =>
        {
            services.AddChord(options =>
            {
                options.UseKafka(ConfigKafka);
            });
        });

        Assert.Equal("Chord configuration error for '(store)': Exactly one store provider must be configured via UseInMemoryStore or UsePostgreSqlStore.", ex.Message);
    }

    [Fact]
    public void AddChord_WithInvalidPostgreSqlStore_Throws()
    {
        var services = CreateServices();

        var ex = Assert.Throws<ChordConfigurationException>(() =>
        {
            services.AddChord(options =>
            {
                options.UseKafka(ConfigKafka);
                options.UsePostgreSqlStore(store =>
                {
                    store.ConnectionString = string.Empty;
                    store.Host = "localhost";
                    store.Port = 5432;
                    store.Database = "chord";
                    store.Username = "postgres";
                });
            });
        });

        Assert.Equal("Chord configuration error for '(store)': PostgreSQL store requires a connection string.", ex.Message);
    }

    private static void ConfigKafka(KafkaOptions kafka)
    {
        kafka.BootstrapServers = "localhost:9092";
        kafka.DefaultTopic = "orders";
    }

    private static void ValidPostgresOptions(PostgreSqlChordStoreOptions store)
    {
        store.ConnectionString = "Host=localhost;Database=chord;Username=postgres;Password=secret;";
        store.Host = "localhost";
        store.Port = 5432;
        store.Database = "chord";
        store.Username = "postgres";
        store.Password = "secret";
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return services;
    }
}
