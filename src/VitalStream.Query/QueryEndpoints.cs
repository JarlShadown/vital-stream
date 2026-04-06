using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http.HttpResults;
using StackExchange.Redis;
using VitalStream.Shared.Enums;
using VitalStream.Shared.Models;

namespace VitalStream.Query;

public static class QueryEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/stream", Stream);
        app.MapGet("/patients/{patientId}/vitals/summary", GetSummary);
        app.MapPatch("/alerts/{alertId}/ack", AckAlert);
    }

    // ── GET /stream ──────────────────────────────────────────────────────────

    private static IResult Stream(
        Guid? patientId,
        VitalType? type,
        VitalSeverity? severity,
        SseManager sseManager,
        CancellationToken ct) =>
        TypedResults.ServerSentEvents(
            StreamVitals(patientId, type, severity, sseManager, ct),
            eventType: "vital");

    private static async IAsyncEnumerable<VitalReading> StreamVitals(
        Guid? patientId,
        VitalType? type,
        VitalSeverity? severity,
        SseManager sseManager,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var (id, reader) = sseManager.Register(patientId, type, severity);
        try
        {
            await foreach (var reading in reader.ReadAllAsync(ct))
                yield return reading;
        }
        finally
        {
            sseManager.Unregister(id);
        }
    }

    // ── GET /patients/{patientId}/vitals/summary ──────────────────────────

    private static Ok<VitalSummary> GetSummary(Guid patientId, VitalStore store)
    {
        var readings = store.GetWindow(patientId);
        var now = DateTimeOffset.UtcNow;

        var byType = readings
            .GroupBy(r => r.Type)
            .Select(g =>
            {
                var values = g.Select(r => r.Value).OrderBy(v => v).ToArray();
                return new VitalTypeSummary(
                    Type:  g.Key,
                    Count: values.Length,
                    P50:   Percentile(values, 50),
                    P90:   Percentile(values, 90),
                    P95:   Percentile(values, 95),
                    P99:   Percentile(values, 99),
                    Min:   values.Length > 0 ? values[0]    : 0,
                    Max:   values.Length > 0 ? values[^1]   : 0);
            })
            .ToList();
        

        return TypedResults.Ok(new VitalSummary(patientId, now - TimeSpan.FromHours(4), now, byType));
    }

    private static double Percentile(double[] sorted, double p)
    {
        if (sorted.Length == 0) return 0;
        var index = p / 100.0 * (sorted.Length - 1);
        var lower = (int)Math.Floor(index);
        var upper = Math.Min((int)Math.Ceiling(index), sorted.Length - 1);
        return lower == upper
            ? sorted[lower]
            : sorted[lower] + (sorted[upper] - sorted[lower]) * (index - lower);
    }

    // ── PATCH /alerts/{alertId}/ack ───────────────────────────────────────

    private static async Task<Results<Ok<AlertAck>, Conflict<string>>> AckAlert(
        Guid alertId,
        IConnectionMultiplexer redis)
    {
        var db = redis.GetDatabase();
        var key = $"alerts:{alertId}:ack";
        var ackedAt = DateTimeOffset.UtcNow;

        // NX: only set if key doesn't exist → idempotency guard
        var set = await db.StringSetAsync(key, ackedAt.ToString("O"), when: When.NotExists);
        if (!set) return TypedResults.Conflict("Alert already acknowledged");

        return TypedResults.Ok(new AlertAck(alertId, ackedAt));
    }
}

// ── Response models ───────────────────────────────────────────────────────────

public record VitalSummary(
    Guid PatientId,
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    List<VitalTypeSummary> ByType);

public record VitalTypeSummary(
    VitalType Type,
    int Count,
    double P50,
    double P90,
    double P95,
    double P99,
    double Min,
    double Max);

public record AlertAck(Guid AlertId, DateTimeOffset AcknowledgedAt);