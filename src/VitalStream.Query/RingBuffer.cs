namespace VitalStream.Query;

/// <summary>
/// Thread-safe fixed-capacity circular buffer. Overwrites the oldest entry when full.
/// </summary>
public sealed class RingBuffer<T>
{
    private readonly T?[] _buffer;
    private int _head;  // next write slot
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

    /// <summary>Returns a snapshot ordered oldest→newest.</summary>
    public T[] ToArray()
    {
        lock (_lock)
        {
            if (_count == 0) return [];
            var result = new T[_count];
            // When full, _head points to the oldest slot
            var start = _count == _buffer.Length ? _head : 0;
            for (var i = 0; i < _count; i++)
                result[i] = _buffer[(start + i) % _buffer.Length]!;
            return result;
        }
    }
}