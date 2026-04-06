using System.Text.Json;
using StackExchange.Redis;
using VitalStream.Shared.Models;

namespace VitalStream.Query;

/// <summary>
/// Background service that subscribes to the Redis pattern vitals:* and
/// forwards each incoming VitalReading to VitalStore and SseManager.
/// </summary>
public sealed class RedisSubscriber(
    IConnectionMultiplexer redis,
    VitalStore store,
    SseManager sse,
    ILogger<RedisSubscriber> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sub = redis.GetSubscriber();
        await sub.SubscribeAsync(RedisChannel.Pattern("vitals:*"), OnMessage);
        logger.LogInformation("Subscribed to Redis pattern vitals:*");

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        finally
        {
            await sub.UnsubscribeAllAsync();
            logger.LogInformation("Unsubscribed from Redis vitals:* pattern");
        }
    }

    private void OnMessage(RedisChannel channel, RedisValue value)
    {
        if (!value.HasValue) return;
        try
        {
            var reading = JsonSerializer.Deserialize(value.ToString(), QueryJsonContext.Default.VitalReading);
            if (reading is null) return;
            store.Add(reading);
            sse.Broadcast(reading);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Malformed VitalReading on channel {Channel}", (string)channel);
        }
    }
}