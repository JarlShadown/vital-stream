namespace VitalStream.Aggregator;

/// <summary>Signals when the Kafka consumer has been assigned partitions.</summary>
public sealed class ReadinessTracker
{
    private volatile bool _ready;
    public bool IsReady => _ready;
    public void SetReady() => _ready = true;
}