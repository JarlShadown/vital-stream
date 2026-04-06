using System.Text.Json;
using Confluent.Kafka;
using StackExchange.Redis;
using VitalStream.Shared.Models;

namespace VitalStream.Aggregator;

/// <summary>
/// Kafka consumer (group 'aggregator') on topic vital-readings.
/// Each message is:
///   1. Published immediately to Redis vitals:{patientId} for low-latency SSE.
///   2. Accumulated into a batch → bulk-inserted to TimescaleDB via unnest().
/// Kafka offsets are committed only after a successful DB flush.
/// </summary>
public sealed class AggregatorWorker(
    IConfiguration config,
    TimeSeriesWriter tsWriter,
    IConnectionMultiplexer redis,
    ReadinessTracker readiness,
    ILogger<AggregatorWorker> logger) : BackgroundService
{
    private const int BatchSize = 100;
    private static readonly TimeSpan BatchTimeout = TimeSpan.FromMilliseconds(500);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumerCfg = new ConsumerConfig
        {
            BootstrapServers = config["Kafka:BootstrapServers"] ?? "localhost:9092",
            GroupId          = "aggregator",
            AutoOffsetReset  = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerCfg).Build();
        consumer.Subscribe(config["Kafka:Topic"] ?? "vital-readings");
        readiness.SetReady();

        var pub   = redis.GetSubscriber();
        var batch = new List<VitalReading>(BatchSize);

        logger.LogInformation("Aggregator started, consuming from {Topic}", config["Kafka:Topic"] ?? "vital-readings");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? result = null;
                try
                {
                    result = consumer.Consume(BatchTimeout);
                }
                catch (ConsumeException ex)
                {
                    logger.LogWarning(ex, "Kafka consume error");
                }

                if (result is not null)
                {
                    var reading = TryDeserialize(result.Message.Value);
                    if (reading is not null)
                    {
                        // Publish immediately so SSE clients get real-time updates
                        await pub.PublishAsync(
                            RedisChannel.Literal($"vitals:{reading.PatientId}"),
                            result.Message.Value);

                        batch.Add(reading);
                    }
                }

                // Flush when batch full OR timeout elapsed with pending data
                var shouldFlush = batch.Count >= BatchSize || (batch.Count > 0 && result is null);
                if (!shouldFlush) continue;

                try
                {
                    await tsWriter.WriteBatchAsync(batch, stoppingToken);
                    consumer.Commit();
                    logger.LogDebug("Flushed batch of {Count} readings", batch.Count);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "DB flush failed; batch of {Count} will be retried", batch.Count);
                    // Don't clear — retry on next loop; don't commit offset
                    continue;
                }

                batch.Clear();
            }
        }
        finally
        {
            // Last partial batch
            if (batch.Count > 0)
            {
                try
                {
                    await tsWriter.WriteBatchAsync(batch, CancellationToken.None);
                    consumer.Commit();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Final flush failed for {Count} readings", batch.Count);
                }
            }
            consumer.Close();
        }
    }

    private VitalReading? TryDeserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<VitalReading>(json);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Malformed VitalReading JSON");
            return null;
        }
    }
}