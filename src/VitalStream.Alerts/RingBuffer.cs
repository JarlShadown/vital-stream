namespace VitalStream.Alerts;

/// <summary>Thread-safe fixed-capacity circular buffer (oldest-first snapshot).</summary>
public sealed class RingBuffer<T>
{
    private readonly T?[] _buffer;
    private int _head;
    private int _count;
    private readonly object _lock = new();

    public RingBuffer(int capacity) => _buffer = new T?[capacity];

    public void Write(T item)
    {
        lock (_lock)
        {
            _buffer[_head] = item;
            _head = (_head + 1) % _buffer.Length;
            if (_count < _buffer.Length) _count++;
        }
    }

    public T[] ToArray()
    {
        lock (_lock)
        {
            if (_count == 0) return [];
            var result = new T[_count];
            var start = _count == _buffer.Length ? _head : 0;
            for (var i = 0; i < _count; i++)
                result[i] = _buffer[(start + i) % _buffer.Length]!;
            return result;
        }
    }
}