using Npgsql;
using NpgsqlTypes;
using VitalStream.Shared.Models;

namespace VitalStream.Aggregator;

/// <summary>
/// Bulk-inserts a batch of VitalReadings into TimescaleDB using
/// a single unnest() query — one round-trip per batch regardless of size.
/// </summary>
public sealed class TimeSeriesWriter(IConfiguration config, ILogger<TimeSeriesWriter> logger)
{
    private readonly string _connStr = config["Database:ConnectionString"]
        ?? "Host=localhost;Port=5432;Database=vitalstream;Username=postgres;Password=postgres";

    private const string Sql = """
        INSERT INTO vital_readings
            (patient_id, device_id, device_type, type, value, unit,
             recorded_at, received_at, severity, correlation_id, notes)
        SELECT * FROM unnest(
            @patient_ids,   @device_ids,   @device_types,
            @types,         @values,       @units,
            @recorded_ats,  @received_ats, @severities,
            @correlation_ids, @notes)
        ON CONFLICT (patient_id, recorded_at, correlation_id) DO NOTHING
        """;

    public async Task WriteBatchAsync(IReadOnlyList<VitalReading> batch, CancellationToken ct)
    {
        if (batch.Count == 0) return;

        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(Sql, conn);

        cmd.Parameters.Add("patient_ids",     NpgsqlDbType.Array | NpgsqlDbType.Uuid)
            .Value = batch.Select(r => r.PatientId).ToArray();

        cmd.Parameters.Add("device_ids",      NpgsqlDbType.Array | NpgsqlDbType.Text)
            .Value = batch.Select(r => r.DeviceId).ToArray();

        cmd.Parameters.Add("device_types",    NpgsqlDbType.Array | NpgsqlDbType.Text)
            .Value = batch.Select(r => r.DeviceType).ToArray();

        cmd.Parameters.Add("types",           NpgsqlDbType.Array | NpgsqlDbType.Integer)
            .Value = batch.Select(r => (int)r.Type).ToArray();

        cmd.Parameters.Add("values",          NpgsqlDbType.Array | NpgsqlDbType.Double)
            .Value = batch.Select(r => r.Value).ToArray();

        cmd.Parameters.Add("units",           NpgsqlDbType.Array | NpgsqlDbType.Text)
            .Value = batch.Select(r => r.Unit).ToArray();

        cmd.Parameters.Add("recorded_ats",    NpgsqlDbType.Array | NpgsqlDbType.TimestampTz)
            .Value = batch.Select(r => r.RecordedAt).ToArray();

        cmd.Parameters.Add("received_ats",    NpgsqlDbType.Array | NpgsqlDbType.TimestampTz)
            .Value = batch.Select(r => r.ReceivedAt).ToArray();

        cmd.Parameters.Add("severities",      NpgsqlDbType.Array | NpgsqlDbType.Integer)
            .Value = batch.Select(r => (int)r.Severity).ToArray();

        cmd.Parameters.Add("correlation_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid)
            .Value = batch.Select(r => r.CorrelationId).ToArray();

        cmd.Parameters.Add("notes",           NpgsqlDbType.Array | NpgsqlDbType.Text)
            .Value = batch.Select(r => (object?)r.Notes ?? DBNull.Value).ToArray();

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        logger.LogDebug("Inserted {Rows}/{Total} vital readings (duplicates skipped)", rows, batch.Count);
    }
}