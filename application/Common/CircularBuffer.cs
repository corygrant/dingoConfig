namespace application.Common;

/// <summary>
/// Thread-safe circular buffer with fixed capacity.
/// Automatically overwrites oldest data when full.
/// Optimized for time-series data collection with O(1) operations.
/// </summary>
public class CircularBuffer<T>
{
    private readonly T[] _buffer;
    private int _head;
    private int _tail;
    private int _count;
    private readonly object _lock = new();

    public int Capacity { get; }
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _count;
            }
        }
    }

    public CircularBuffer(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentException("Capacity must be greater than zero", nameof(capacity));

        Capacity = capacity;
        _buffer = new T[capacity];
        _head = 0;
        _tail = 0;
        _count = 0;
    }

    /// <summary>
    /// Adds an item to the buffer. If full, overwrites the oldest item.
    /// Thread-safe with O(1) complexity.
    /// </summary>
    public void Add(T item)
    {
        lock (_lock)
        {
            _buffer[_tail] = item;
            _tail = (_tail + 1) % Capacity;

            if (_count < Capacity)
            {
                _count++;
            }
            else
            {
                // Buffer full, move head forward (overwrite oldest)
                _head = (_head + 1) % Capacity;
            }
        }
    }

    /// <summary>
    /// Returns all items in chronological order (oldest first).
    /// Thread-safe, creates a new list.
    /// </summary>
    public List<T> GetAll()
    {
        lock (_lock)
        {
            var result = new List<T>(_count);

            if (_count == 0)
                return result;

            // Read from head to tail in chronological order
            for (int i = 0; i < _count; i++)
            {
                int index = (_head + i) % Capacity;
                result.Add(_buffer[index]);
            }

            return result;
        }
    }

    /// <summary>
    /// Clears all items from the buffer.
    /// Thread-safe with O(1) complexity.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _head = 0;
            _tail = 0;
            _count = 0;
        }
    }
}