using VitalStream.Shared.Enums;
using VitalStream.Shared.Models;

namespace VitalStream.Alerts;

/// <summary>
/// Detects a falling SpO2 trend within a 10-minute sliding window.
/// Strategy: split the window in two halves; if the first-half mean minus
/// the second-half mean exceeds the threshold, the trend is falling.
/// Requires at least 4 readings to reduce noise.
/// </summary>
public class TrendDetector
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);
    private const double FallThresholdPct = 2.0;   // percentage points
    private const int MinReadings = 4;

    /// <summary>
    /// Returns true when a sustained SpO2 fall > 2 pp is detected in the last 10 min.
    /// <paramref name="allReadings"/> is the full patient buffer (any vital type).
    /// </summary>
    public bool IsSpo2Falling(VitalReading[] allReadings)
    {
        var cutoff = DateTimeOffset.UtcNow - Window;
        var window = allReadings
            .Where(r => r.Type == VitalType.SpO2 && r.ReceivedAt >= cutoff)
            .OrderBy(r => r.ReceivedAt)
            .ToArray();

        if (window.Length < MinReadings) return false;

        var half       = window.Length / 2;
        var firstMean  = window[..half].Average(r => r.Value);
        var secondMean = window[half..].Average(r => r.Value);

        return firstMean - secondMean > FallThresholdPct;
    }
}