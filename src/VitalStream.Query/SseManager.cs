using System.Collections.Concurrent;
using System.Threading.Channels;
using VitalStream.Shared.Enums;
using VitalStream.Shared.Models;

namespace VitalStream.Query;

/// <summary>
/// Manages one BoundedChannel per connected SSE client.
/// Broadcast fanouts readings to every channel whose filter matches.
/// </summary>
public sealed class SseManager
{
    private readonly ConcurrentDictionary<string, (SseFilter Filter, Channel<VitalReading> Channel)> _clients = new();

    public (string Id, ChannelReader<VitalReading> Reader) Register(
        Guid? patientId, VitalType? type, VitalSeverity? severity)
    {
        var id = Guid.NewGuid().ToString("N");
        var channel = Channel.CreateBounded<VitalReading>(new BoundedChannelOptions(256)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = false,
            SingleReader = true
        });
        _clients[id] = (new SseFilter(patientId, type, severity), channel);
        return (id, channel.Reader);
    }

    public void Unregister(string id)
    {
        if (_clients.TryRemove(id, out var entry))
            entry.Channel.Writer.TryComplete();
    }

    public int ActiveConnections => _clients.Count;

    public void Broadcast(VitalReading reading)
    {
        foreach (var (_, (filter, channel)) in _clients)
        {
            if (filter.Matches(reading))
                channel.Writer.TryWrite(reading);
        }
    }
}

public sealed record SseFilter(Guid? PatientId, VitalType? Type, VitalSeverity? Severity)
{
    public bool Matches(VitalReading r) =>
        (PatientId is null || r.PatientId == PatientId) &&
        (Type     is null || r.Type      == Type)      &&
        (Severity is null || r.Severity  == Severity);
}