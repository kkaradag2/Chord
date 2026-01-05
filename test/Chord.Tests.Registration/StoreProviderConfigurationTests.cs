using Chord;
using Chord.Messaging.Kafka;
using Chord.Store.InMemory;
using Chord.Store.PostgreSql;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Chord.Tests.Registration;

public class StoreProviderConfigurationTests
{
    private const string ConnectionString = "Host=localhost;Port=5432;Username=ratio_user;Password=ratio_pass;Database=chord_registration_test";
    private const string Host = "localhost";
    private const int Port = 5432;
    private const string Database = "chord_registration_test";
    private const string Username = "ratio_user";
    private const string Password = "ratio_pass";

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
    public void UsePostgreSqlStore_CreatesSchema()
    {
        ResetDatabase();

        var services = CreateServices();
        services.AddChord(options =>
        {
            options.UseKafka(ConfigKafka);
            options.UsePostgreSqlStore(ConfigPostgres);
        });

        using var provider = services.BuildServiceProvider();
        AssertTablesExist();
    }

    [Fact]
    public void UsePostgreSqlStore_IsIdempotent()
    {
        ResetDatabase();

        var services = CreateServices();
        services.AddChord(options =>
        {
            options.UseKafka(ConfigKafka);
            options.UsePostgreSqlStore(ConfigPostgres);
        });

        using (var provider = services.BuildServiceProvider())
        {
            AssertTablesExist();
        }

        // Run the configuration again to ensure no exception occurs.
        services = CreateServices();
        services.AddChord(options =>
        {
            options.UseKafka(ConfigKafka);
            options.UsePostgreSqlStore(ConfigPostgres);
        });

        using var secondProvider = services.BuildServiceProvider();
        AssertTablesExist();
    }

    [Fact]
    public void AddChord_WithMultipleStores_Throws()
    {
        ResetDatabase();

        var services = CreateServices();

        var ex = Assert.Throws<ChordConfigurationException>(() =>
        {
            services.AddChord(options =>
            {
                options.UseKafka(ConfigKafka);
                options.UseInMemoryStore();
                options.UsePostgreSqlStore(ConfigPostgres);
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
                    store.Host = Host;
                    store.Port = Port;
                    store.Database = Database;
                    store.Username = Username;
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

    private static void ConfigPostgres(PostgreSqlChordStoreOptions store)
    {
        store.ConnectionString = ConnectionString;
        store.Host = Host;
        store.Port = Port;
        store.Database = Database;
        store.Username = Username;
        store.Password = Password;
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return services;
    }

    private static void ResetDatabase()
    {
        using var connection = new NpgsqlConnection(ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            DROP TABLE IF EXISTS chord_message_logs CASCADE;
            DROP TABLE IF EXISTS chord_step_instances CASCADE;
            DROP TABLE IF EXISTS chord_flow_instances CASCADE;
            DROP TABLE IF EXISTS chord_schema_version CASCADE;";
        command.ExecuteNonQuery();
    }

    private static void AssertTablesExist()
    {
        using var connection = new NpgsqlConnection(ConnectionString);
        connection.Open();

        foreach (var table in new[]
                 {
                     "chord_flow_instances",
                     "chord_step_instances",
                     "chord_message_logs",
                     "chord_schema_version"
                 })
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT to_regclass(@name)::text";
            command.Parameters.AddWithValue("name", table);
            var result = command.ExecuteScalar() as string;
            Assert.Equal(table, result);
        }
    }
}
