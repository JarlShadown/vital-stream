using StackExchange.Redis;

namespace VitalStream.Alerts;

/// <summary>
/// Redis-backed store for clinical alerts.
///
/// Alert hash  →  alert:{alertId}    HSET fields: patientId, message, severity, triggeredAt
/// Unacked set →  alerts:critical:unacked   ZADD score=triggeredAt epoch, member=alertId
/// Ack key     →  alerts:{alertId}:ack      SET by Query service PATCH /alerts/{id}/ack
/// </summary>
public sealed class AlertStore(IConnectionMultiplexer redis)
{
    private const string UnackedKey = "alerts:critical:unacked";

    public async Task SaveAsync(Guid alertId, Guid patientId, string message, string severity)
    {
        var db = redis.GetDatabase();
        var now = DateTimeOffset.UtcNow;

        var tran = db.CreateTransaction();

        _ = tran.HashSetAsync($"alert:{alertId}", [
            new HashEntry("patientId",   patientId.ToString()),
            new HashEntry("message",     message),
            new HashEntry("severity",    severity),
            new HashEntry("triggeredAt", now.ToString("O"))
        ]);

        if (string.Equals(severity, "Critical", StringComparison.OrdinalIgnoreCase))
        {
            _ = tran.SortedSetAddAsync(UnackedKey, alertId.ToString(), now.ToUnixTimeSeconds());
        }

        await tran.ExecuteAsync();
    }

    /// <summary>
    /// Returns all Critical alerts triggered before <paramref name="before"/> that have no ack key.
    /// </summary>
    public async Task<IReadOnlyList<UnackedAlert>> GetUnackedCriticalAsync(DateTimeOffset before)
    {
        var db = redis.GetDatabase();
        var members = await db.SortedSetRangeByScoreAsync(
            UnackedKey,
            start: 0,
            stop:  before.ToUnixTimeSeconds());

        var result = new List<UnackedAlert>();
        foreach (var member in members)
        {
            var alertId = member.ToString();
            var isAcked = await db.KeyExistsAsync($"alerts:{alertId}:ack");
            if (isAcked)
            {
                // Lazily clean up acknowledged alerts from the set
                await db.SortedSetRemoveAsync(UnackedKey, alertId);
                continue;
            }

            var hash = await db.HashGetAllAsync($"alert:{alertId}");
            if (hash.Length == 0) continue;

            var fields = hash.ToDictionary(e => e.Name.ToString(), e => e.Value.ToString());
            result.Add(new UnackedAlert(
                AlertId:     Guid.Parse(alertId),
                PatientId:   Guid.Parse(fields.GetValueOrDefault("patientId", Guid.Empty.ToString())),
                Message:     fields.GetValueOrDefault("message", ""),
                TriggeredAt: DateTimeOffset.Parse(fields.GetValueOrDefault("triggeredAt", DateTimeOffset.UtcNow.ToString("O")))));
        }

        return result;
    }

    public async Task RemoveFromUnackedAsync(Guid alertId) =>
        await redis.GetDatabase().SortedSetRemoveAsync(UnackedKey, alertId.ToString());
}

public record UnackedAlert(Guid AlertId, Guid PatientId, string Message, DateTimeOffset TriggeredAt);