using System.Linq;
using System.Text.Json;
using Chord;
using Chord.Messaging.RabitMQ;
using Chord.Store.PostgreSql;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Xunit;

namespace Chord.Tests.Store.PostgreSql;

public class PostgreSqlStoreIntegrationTests
{
    private const string Host = "localhost";
    private const int Port = 5432;
    private const string Username = "ratio_user";
    private const string Password = "ratio_pass";
    private const string Database = "chord_store_integration_test";
    private static readonly string ConnectionString = $"Host={Host};Port={Port};Username={Username};Password={Password};Database={Database}";

    [Fact]
    public async Task FlowExecution_PersistsStateAndMessageLogs()
    {
        await PostgreSqlTestDatabase.EnsureExistsAsync(ConnectionString, Username, Password, Host, Port);

        var flowPath = Path.Combine(AppContext.BaseDirectory, "TestData", "store-flow.yaml");
        await using var environment = await PostgresFlowEnvironment.CreateAsync(ConnectionString, flowPath);
        await PostgreSqlTestDatabase.ClearTablesAsync(ConnectionString);

        var runtime = environment.ServiceProvider.GetRequiredService<ChordFlowRuntime>();
        var bus = environment.ServiceProvider.GetRequiredService<TestRabbitMqMessageBus>();

        await runtime.StartFlowAsync("StoreTestFlow", new { orderId = 777 });

        var correlationId = bus.PublishedMessages.First().Headers[ChordMessageHeaders.CorrelationId];

        await bus.EmitCompletionAsync("queue.store-completed", correlationId, "validate", new { orderId = 777 });
        await bus.EmitCompletionAsync("queue.store-completed", correlationId, "charge", new { orderId = 777 });
        await bus.EmitCompletionAsync("queue.store-completed", correlationId, "ship", new { orderId = 777 });

        Assert.Equal(0, runtime.ActiveFlowCount);

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        var flowRow = await QueryFlowInstanceAsync(connection, correlationId);
        Assert.Equal("StoreTestFlow", flowRow.FlowName);
        Assert.Equal("Completed", flowRow.Status);
        Assert.NotNull(flowRow.CompletedAt);
        Assert.True(flowRow.DurationMs is >= 0);

        var steps = await QueryStepInstancesAsync(connection, flowRow.Id);
        Assert.Equal(new[] { "validate", "charge", "ship" }, steps.Select(s => s.StepId));
        Assert.All(steps, step =>
        {
            Assert.Equal("Completed", step.Status);
            Assert.NotNull(step.CompletedAt);
            Assert.True(step.DurationMs is >= 0);
        });

        var logs = await QueryMessageLogsAsync(connection, flowRow.Id);
        Assert.Equal(6, logs.Count);
        Assert.Equal(
            new[]
            {
                ("OUT", "queue.validate"),
                ("IN", "queue.store-completed"),
                ("OUT", "queue.charge"),
                ("IN", "queue.store-completed"),
                ("OUT", "queue.ship"),
                ("IN", "queue.store-completed")
            },
            logs.Select(l => (l.Direction, l.QueueName)).ToArray());

        foreach (var log in logs)
        {
            Assert.True(log.CreatedAt > DateTimeOffset.MinValue);
            using var doc = JsonDocument.Parse(log.HeadersJson ?? "{}");
            Assert.True(doc.RootElement.TryGetProperty(ChordMessageHeaders.CorrelationId, out var correlationProperty));
            Assert.Equal(correlationId, correlationProperty.GetString());
        }
    }

    private static async Task<FlowRow> QueryFlowInstanceAsync(NpgsqlConnection connection, string correlationId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, flow_name, correlation_id, status, started_at, completed_at, duration_ms FROM chord_flow_instances WHERE correlation_id = @cid";
        command.Parameters.AddWithValue("cid", correlationId);

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "Flow instance row not found.");

        return new FlowRow(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetFieldValue<DateTime>(4),
            reader.IsDBNull(5) ? (DateTime?)null : reader.GetFieldValue<DateTime>(5),
            reader.IsDBNull(6) ? (long?)null : reader.GetInt64(6));
    }

    private static async Task<IReadOnlyList<StepRow>> QueryStepInstancesAsync(NpgsqlConnection connection, Guid flowInstanceId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT step_id, status, started_at, completed_at, duration_ms
            FROM chord_step_instances
            WHERE flow_instance_id = @flow
            ORDER BY started_at ASC;";
        command.Parameters.AddWithValue("flow", flowInstanceId);

        await using var reader = await command.ExecuteReaderAsync();
        var results = new List<StepRow>();
        while (await reader.ReadAsync())
        {
            results.Add(new StepRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetFieldValue<DateTime>(2),
                reader.IsDBNull(3) ? (DateTime?)null : reader.GetFieldValue<DateTime>(3),
                reader.IsDBNull(4) ? (long?)null : reader.GetInt64(4)));
        }

        return results;
    }

    private static async Task<IReadOnlyList<MessageLogRow>> QueryMessageLogsAsync(NpgsqlConnection connection, Guid flowInstanceId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT direction, queue_name, headers::text, created_at
            FROM chord_message_logs
            WHERE flow_instance_id = @flow
            ORDER BY created_at ASC;";
        command.Parameters.AddWithValue("flow", flowInstanceId);

        await using var reader = await command.ExecuteReaderAsync();
        var results = new List<MessageLogRow>();
        while (await reader.ReadAsync())
        {
            results.Add(new MessageLogRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? "{}" : reader.GetString(2),
                reader.GetFieldValue<DateTime>(3)));
        }

        return results;
    }

    private sealed record FlowRow(Guid Id, string FlowName, string CorrelationId, string Status, DateTime StartedAt, DateTime? CompletedAt, long? DurationMs);

    private sealed record StepRow(string StepId, string Status, DateTime StartedAt, DateTime? CompletedAt, long? DurationMs);

    private sealed record MessageLogRow(string Direction, string QueueName, string HeadersJson, DateTime CreatedAt);

    private sealed class PostgresFlowEnvironment : IAsyncDisposable
    {
        public required ServiceProvider ServiceProvider { get; init; }
        private IReadOnlyCollection<IHostedService>? HostedServices { get; init; }

        public static async Task<PostgresFlowEnvironment> CreateAsync(string connectionString, string flowPath)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<TestRabbitMqMessageBus>();

            services.AddChord(options =>
            {
                options.UseYamlFlows(flowPath);
                options.UseRabbitMq(rabbit =>
                {
                    rabbit.HostName = Host;
                    rabbit.Port = 5672;
                    rabbit.UserName = "guest";
                    rabbit.Password = "guest";
                    rabbit.VirtualHost = "/";
                    rabbit.SkipConnectivityCheck = true;
                });
                options.UsePostgreSqlStore(store =>
                {
                    store.ConnectionString = connectionString;
                    store.Host = Host;
                    store.Port = Port;
                    store.Database = Database;
                    store.Username = Username;
                    store.Password = Password;
                });
            });

            services.AddSingleton<IChordMessageBus>(sp => sp.GetRequiredService<TestRabbitMqMessageBus>());

            var provider = services.BuildServiceProvider();
            var hosted = provider.GetServices<IHostedService>().ToArray();
            foreach (var hostedService in hosted)
            {
                await hostedService.StartAsync(CancellationToken.None);
            }

            return new PostgresFlowEnvironment
            {
                ServiceProvider = provider,
                HostedServices = hosted
            };
        }

        public async ValueTask DisposeAsync()
        {
            if (HostedServices is { Count: > 0 })
            {
                foreach (var hosted in HostedServices)
                {
                    await hosted.StopAsync(CancellationToken.None);
                }
            }

            if (ServiceProvider is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else
            {
                ServiceProvider.Dispose();
            }
        }
    }

    private static class PostgreSqlTestDatabase
    {
        public static async Task EnsureExistsAsync(string connectionString, string username, string password, string host, int port)
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            if (await DatabaseExistsAsync(builder.Database, username, password, host, port))
            {
                return;
            }

            var adminBuilder = new NpgsqlConnectionStringBuilder
            {
                Host = host,
                Port = port,
                Username = username,
                Password = password,
                Database = "postgres"
            };

            await using var connection = new NpgsqlConnection(adminBuilder.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE \"{builder.Database}\"";
            await command.ExecuteNonQueryAsync();
        }

        public static async Task ClearTablesAsync(string connectionString)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = @"
                TRUNCATE TABLE chord_message_logs RESTART IDENTITY CASCADE;
                TRUNCATE TABLE chord_step_instances RESTART IDENTITY CASCADE;
                TRUNCATE TABLE chord_flow_instances RESTART IDENTITY CASCADE;";
            await command.ExecuteNonQueryAsync();
        }

        private static async Task<bool> DatabaseExistsAsync(string databaseName, string username, string password, string host, int port)
        {
            var adminBuilder = new NpgsqlConnectionStringBuilder
            {
                Host = host,
                Port = port,
                Username = username,
                Password = password,
                Database = "postgres"
            };

            await using var connection = new NpgsqlConnection(adminBuilder.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1 FROM pg_database WHERE datname = @name";
            command.Parameters.AddWithValue("name", databaseName);
            var result = await command.ExecuteScalarAsync();
            return result is not null;
        }
    }
}
