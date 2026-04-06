namespace VitalStream.Alerts;

/// <summary>Signals when the Redis subscription is active.</summary>
public sealed class ReadinessTracker
{
    private volatile bool _ready;
    public bool IsReady => _ready;
    public void SetReady() => _ready = true;
}