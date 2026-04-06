using System.Collections.Concurrent;
using VitalStream.Shared.Models;

namespace VitalStream.Query;

/// <summary>
/// Per-patient in-memory store backed by a RingBuffer.
/// GetWindow returns only readings within the last 4 hours.
/// </summary>
public sealed class VitalStore
{
    private static readonly TimeSpan Window = TimeSpan.FromHours(4);

    // Upper bound: ~5 vital types × 1 reading/sec × 4h = 72 000; cap at 20 000 per patient.
    private const int BufferCapacity = 20_000;

    private readonly ConcurrentDictionary<Guid, RingBuffer<VitalReading>> _buffers = new();

    public void Add(VitalReading reading)
    {
        var buffer = _buffers.GetOrAdd(reading.PatientId, _ => new RingBuffer<VitalReading>(BufferCapacity));
        buffer.Write(reading);
    }

    public IReadOnlyList<VitalReading> GetWindow(Guid patientId)
    {
        if (!_buffers.TryGetValue(patientId, out var buffer)) return [];
        var cutoff = DateTimeOffset.UtcNow - Window;
        return Array.FindAll(buffer.ToArray(), r => r.ReceivedAt >= cutoff);
    }

    public IEnumerable<Guid> GetActivePatients() => _buffers.Keys;
}