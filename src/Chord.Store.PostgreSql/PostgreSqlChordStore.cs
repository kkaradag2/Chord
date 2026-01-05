using System;
using System.Collections.Generic;
using System.Data;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Chord;
using Npgsql;
using NpgsqlTypes;

namespace Chord.Store.PostgreSql;

/// <summary>
/// PostgreSQL-backed implementation of <see cref="IChordStore"/>.
/// </summary>
public sealed class PostgreSqlChordStore : IChordStore
{
    private readonly PostgreSqlChordStoreOptions _options;

    public PostgreSqlChordStore(PostgreSqlChordStoreOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<FlowInstanceRecord> CreateFlowInstanceAsync(string flowName, string correlationId, DateTimeOffset startedAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flowName);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        var id = Guid.NewGuid();
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO chord_flow_instances (id, flow_name, correlation_id, status, started_at)
            VALUES (@id, @flow_name, @correlation_id, @status, @started_at);
            """;
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("flow_name", flowName);
        command.Parameters.AddWithValue("correlation_id", correlationId);
        command.Parameters.AddWithValue("status", FlowInstanceStatus.Running.ToString());
        command.Parameters.AddWithValue("started_at", startedAtUtc.UtcDateTime);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        return new FlowInstanceRecord(id, flowName, correlationId, FlowInstanceStatus.Running, startedAtUtc, null, null);
    }

    public async Task<FlowInstanceRecord> CompleteFlowInstanceAsync(Guid flowInstanceId, DateTimeOffset completedAtUtc, long durationMilliseconds, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE chord_flow_instances
            SET status = @status,
                completed_at = @completed_at,
                duration_ms = @duration_ms
            WHERE id = @id
            RETURNING id, flow_name, correlation_id, status, started_at, completed_at, duration_ms;
            """;

        command.Parameters.AddWithValue("status", FlowInstanceStatus.Completed.ToString());
        command.Parameters.AddWithValue("completed_at", completedAtUtc.UtcDateTime);
        command.Parameters.AddWithValue("duration_ms", durationMilliseconds);
        command.Parameters.AddWithValue("id", flowInstanceId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException($"Flow instance '{flowInstanceId}' was not found.");
        }

        return ReadFlow(reader);
    }

    public async Task<StepInstanceRecord> CreateStepInstanceAsync(Guid flowInstanceId, string stepId, DateTimeOffset startedAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stepId);

        var id = Guid.NewGuid();
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO chord_step_instances (id, flow_instance_id, step_id, status, started_at)
            VALUES (@id, @flow_instance_id, @step_id, @status, @started_at);
            """;

        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("flow_instance_id", flowInstanceId);
        command.Parameters.AddWithValue("step_id", stepId);
        command.Parameters.AddWithValue("status", StepInstanceStatus.Running.ToString());
        command.Parameters.AddWithValue("started_at", startedAtUtc.UtcDateTime);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        return new StepInstanceRecord(id, flowInstanceId, stepId, StepInstanceStatus.Running, startedAtUtc, null, null);
    }

    public async Task<StepInstanceRecord> CompleteStepInstanceAsync(Guid stepInstanceId, DateTimeOffset completedAtUtc, long durationMilliseconds, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE chord_step_instances
            SET status = @status,
                completed_at = @completed_at,
                duration_ms = @duration_ms
            WHERE id = @id
            RETURNING id, flow_instance_id, step_id, status, started_at, completed_at, duration_ms;
            """;

        command.Parameters.AddWithValue("status", StepInstanceStatus.Completed.ToString());
        command.Parameters.AddWithValue("completed_at", completedAtUtc.UtcDateTime);
        command.Parameters.AddWithValue("duration_ms", durationMilliseconds);
        command.Parameters.AddWithValue("id", stepInstanceId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException($"Step instance '{stepInstanceId}' was not found.");
        }

        return ReadStep(reader);
    }

    public async Task<IReadOnlyList<FlowInstanceRecord>> QueryFlowInstancesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, flow_name, correlation_id, status, started_at, completed_at, duration_ms
            FROM chord_flow_instances
            ORDER BY started_at ASC;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var results = new List<FlowInstanceRecord>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(ReadFlow(reader));
        }

        return results;
    }

    public async Task<IReadOnlyList<StepInstanceRecord>> QueryStepInstancesAsync(Guid flowInstanceId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, flow_instance_id, step_id, status, started_at, completed_at, duration_ms
            FROM chord_step_instances
            WHERE flow_instance_id = @flow_id
            ORDER BY started_at ASC;
            """;
        command.Parameters.AddWithValue("flow_id", flowInstanceId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var results = new List<StepInstanceRecord>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(ReadStep(reader));
        }

        return results;
    }

    public async Task LogMessageAsync(ChordMessageDirection direction, Guid flowInstanceId, string queueName, string? stepId, IReadOnlyDictionary<string, string>? headers, DateTimeOffset createdAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO chord_message_logs (id, flow_instance_id, step_id, direction, queue_name, headers, created_at)
            VALUES (@id, @flow_instance_id, @step_id, @direction, @queue_name, @headers, @created_at);
            """;

        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("flow_instance_id", flowInstanceId);
        command.Parameters.AddWithValue("step_id", stepId is null ? DBNull.Value : stepId);
        command.Parameters.AddWithValue("direction", ToDirectionString(direction));
        command.Parameters.AddWithValue("queue_name", queueName);
        var headersParameter = command.Parameters.Add("headers", NpgsqlDbType.Jsonb);
        headersParameter.Value = headers is null
            ? DBNull.Value
            : JsonSerializer.Serialize(headers);
        command.Parameters.AddWithValue("created_at", createdAtUtc.UtcDateTime);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<MessageLogRecord>> QueryMessageLogsAsync(Guid flowInstanceId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, flow_instance_id, step_id, direction, queue_name, headers, created_at
            FROM chord_message_logs
            WHERE flow_instance_id = @flow_id
            ORDER BY created_at ASC;
            """;
        command.Parameters.AddWithValue("flow_id", flowInstanceId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var results = new List<MessageLogRecord>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(ReadMessageLog(reader));
        }

        return results;
    }

    private async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private static FlowInstanceRecord ReadFlow(NpgsqlDataReader reader)
    {
        var id = reader.GetGuid(0);
        var flowName = reader.GetString(1);
        var correlationId = reader.GetString(2);
        var status = ParseFlowStatus(reader.GetString(3));
        var startedAt = ToUtc(reader.GetFieldValue<DateTime>(4));
        var completedAt = reader.IsDBNull(5) ? (DateTimeOffset?)null : ToUtc(reader.GetFieldValue<DateTime>(5));
        var duration = reader.IsDBNull(6) ? (long?)null : reader.GetInt64(6);

        return new FlowInstanceRecord(id, flowName, correlationId, status, startedAt, completedAt, duration);
    }

    private static StepInstanceRecord ReadStep(NpgsqlDataReader reader)
    {
        var id = reader.GetGuid(0);
        var flowInstanceId = reader.GetGuid(1);
        var stepId = reader.GetString(2);
        var status = ParseStepStatus(reader.GetString(3));
        var startedAt = ToUtc(reader.GetFieldValue<DateTime>(4));
        var completedAt = reader.IsDBNull(5) ? (DateTimeOffset?)null : ToUtc(reader.GetFieldValue<DateTime>(5));
        var duration = reader.IsDBNull(6) ? (long?)null : reader.GetInt64(6);

        return new StepInstanceRecord(id, flowInstanceId, stepId, status, startedAt, completedAt, duration);
    }

    private static FlowInstanceStatus ParseFlowStatus(string value)
    {
        return Enum.TryParse<FlowInstanceStatus>(value, true, out var status)
            ? status
            : throw new InvalidOperationException($"Unrecognized flow status '{value}'.");
    }

    private static StepInstanceStatus ParseStepStatus(string value)
    {
        return Enum.TryParse<StepInstanceStatus>(value, true, out var status)
            ? status
            : throw new InvalidOperationException($"Unrecognized step status '{value}'.");
    }

    private static DateTimeOffset ToUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? new DateTimeOffset(value)
            : new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }

    private static MessageLogRecord ReadMessageLog(NpgsqlDataReader reader)
    {
        var id = reader.GetGuid(0);
        var flowInstanceId = reader.GetGuid(1);
        var stepId = reader.IsDBNull(2) ? null : reader.GetString(2);
        var direction = ParseDirection(reader.GetString(3));
        var queueName = reader.GetString(4);
        IReadOnlyDictionary<string, string>? headers = null;
        if (!reader.IsDBNull(5))
        {
            var json = reader.GetFieldValue<string>(5);
            headers = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var createdAt = ToUtc(reader.GetFieldValue<DateTime>(6));
        return new MessageLogRecord(id, flowInstanceId, stepId, direction, queueName, headers, createdAt);
    }

    private static string ToDirectionString(ChordMessageDirection direction) => direction switch
    {
        ChordMessageDirection.Outbound => "OUT",
        ChordMessageDirection.Inbound => "IN",
        _ => throw new ArgumentOutOfRangeException(nameof(direction))
    };

    private static ChordMessageDirection ParseDirection(string value) => value.ToUpperInvariant() switch
    {
        "OUT" => ChordMessageDirection.Outbound,
        "IN" => ChordMessageDirection.Inbound,
        _ => throw new InvalidOperationException($"Unrecognized direction '{value}'.")
    };
}
