namespace VitalStream.Shared.Models;

public record PatientDevice
{
    public required string DeviceId { get; init; }
    public required Guid PatientId { get; init; }
    public required string DeviceType { get; init; }
    public required DateTimeOffset RegisteredAt { get; init; }
    public required string WebHookSecret { get; init; }
    public required bool IsActive { get; init; }
}