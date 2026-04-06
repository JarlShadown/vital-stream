using VitalStream.Shared.Domain;
using VitalStream.Shared.Enums;

namespace VitalStream.Shared.Models;

public record VitalReading
{
    public required Guid PatientId { get; init; }
    public required string DeviceId { get; init; }
    public required string DeviceType { get; init; }
    public required VitalType Type { get; init; }
    public required double Value { get; init; }
    public required string Unit  { get; init; }
    public required DateTimeOffset RecordedAt { get; init; }
    public required DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.UtcNow;
    public VitalSeverity Severity => ClinicalThresholds.Evaluate(Type, Value);
    public required Guid CorrelationId { get; init; }
    public required string? Notes { get; init; }
}