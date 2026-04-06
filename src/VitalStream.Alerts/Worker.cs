using System.Collections.Concurrent;
using System.Text.Json;
using StackExchange.Redis;
using VitalStream.Shared.Models;

namespace VitalStream.Alerts;

/// <summary>
/// Subscribes to Redis vitals:* pattern.
/// For each incoming VitalReading:
///   - Appends to the per-patient RingBuffer.
///   - Runs TrendDetector; fires a Critical alert on sustained SpO2 fall.
/// A per-patient cooldown (10 min) prevents alert storms.
/// </summary>
public sealed class AlertsWorker(
    IConnectionMultiplexer redis,
    AlertStore alertStore,
    TrendDetector trendDetector,
    ReadinessTracker readiness,
    ILogger<AlertsWorker> logger) : BackgroundService
{
    // 4-hour window × 5 types × 1 reading/sec upper bound
    private const int BufferCapacity = 20_000;
    private static readonly TimeSpan AlertCooldown = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<Guid, RingBuffer<VitalReading>> _buffers  = new();
    private readonly ConcurrentDictionary<Guid, DateTimeOffset>           _cooldowns = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sub = redis.GetSubscriber();
        await sub.SubscribeAsync(RedisChannel.Pattern("vitals:*"), OnMessage);
        readiness.SetReady();
        logger.LogInformation("AlertsWorker subscribed to vitals:*");

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        finally
        {
            await sub.UnsubscribeAllAsync();
        }
    }

    private void OnMessage(RedisChannel _, RedisValue value)
    {
        if (!value.HasValue) return;
        VitalReading? reading;
        try
        {
            reading = JsonSerializer.Deserialize<VitalReading>(value.ToString());
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Malformed VitalReading on vitals:* channel");
            return;
        }

        if (reading is null) return;

        var buffer = _buffers.GetOrAdd(reading.PatientId, _ => new RingBuffer<VitalReading>(BufferCapacity));
        buffer.Write(reading);

        EvaluateTrends(reading.PatientId, buffer.ToArray());
    }

    private void EvaluateTrends(Guid patientId, VitalReading[] snapshot)
    {
        if (!trendDetector.IsSpo2Falling(snapshot)) return;

        // Cooldown check — avoid alert storms
        var now = DateTimeOffset.UtcNow;
        if (_cooldowns.TryGetValue(patientId, out var lastAlert) && now - lastAlert < AlertCooldown)
            return;

        _cooldowns[patientId] = now;

        var alertId = Guid.NewGuid();
        const string message  = "SpO2 falling trend detected: >2 pp drop within 10 minutes";
        const string severity = "Critical";

        logger.LogWarning("ALERT [{Severity}] Patient={PatientId} — {Message}", severity, patientId, message);

        // Fire-and-forget: don't block the Redis message callback
        _ = alertStore.SaveAsync(alertId, patientId, message, severity);
    }
}