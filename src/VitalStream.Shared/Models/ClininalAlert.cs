using VitalStream.Shared.Models;

namespace VitalStream.Shared;

public record ClinicalAlert
{
    public required Guid AlertId { get; init; }
    public required Guid PatientId { get; init; }
    public required VitalReading TriggerReading { get; init; }
    public required string Message { get; init; }
    public required DateTimeOffset TriggeredAt { get; init; }
    public required DateTimeOffset AcknowledgedAt { get; init; }
}