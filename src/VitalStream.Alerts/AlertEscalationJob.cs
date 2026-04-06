using StackExchange.Redis;

namespace VitalStream.Alerts;

/// <summary>
/// Background job that runs every 60 seconds.
/// Finds Critical alerts with no acknowledgement after 5 minutes and escalates them:
///   - Logs at Critical level.
///   - Publishes to Redis channel alerts:escalated for downstream consumers.
/// </summary>
public sealed class AlertEscalationJob(
    AlertStore alertStore,
    IConnectionMultiplexer redis,
    ILogger<AlertEscalationJob> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval    = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan EscalationWindow = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("AlertEscalationJob started (interval={Interval})", CheckInterval);

        using var timer = new PeriodicTimer(CheckInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CheckAndEscalateAsync(stoppingToken);
        }
    }

    private async Task CheckAndEscalateAsync(CancellationToken ct)
    {
        var escalateBefore = DateTimeOffset.UtcNow - EscalationWindow;

        IReadOnlyList<UnackedAlert> unacked;
        try
        {
            unacked = await alertStore.GetUnackedCriticalAsync(escalateBefore);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to query unacked alerts");
            return;
        }

        if (unacked.Count == 0) return;

        var pub = redis.GetSubscriber();

        foreach (var alert in unacked)
        {
            logger.LogCritical(
                "ESCALATED — AlertId={AlertId} PatientId={PatientId} TriggeredAt={TriggeredAt} Message={Message}",
                alert.AlertId, alert.PatientId, alert.TriggeredAt, alert.Message);

            try
            {
                await pub.PublishAsync(
                    RedisChannel.Literal("alerts:escalated"),
                    $"{{\"alertId\":\"{alert.AlertId}\",\"patientId\":\"{alert.PatientId}\"," +
                    $"\"triggeredAt\":\"{alert.TriggeredAt:O}\",\"message\":\"{alert.Message}\"}}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to publish escalation for {AlertId}", alert.AlertId);
            }
        }

        logger.LogWarning("Escalated {Count} unacknowledged Critical alert(s)", unacked.Count);
    }
}