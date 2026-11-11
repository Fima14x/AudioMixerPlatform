namespace AudioMixer.EngineProbe.Audio;

/// <summary>
/// Bounded, lock-protected single-producer/single-consumer float ring.
/// On overflow the oldest samples are dropped so latency cannot grow without bound.
/// </summary>
public sealed class FloatRingBuffer
{
    private readonly float[] _buffer;
    private readonly object _gate = new();
    private long _read;
    private long _write;
    private long _droppedSamples;

    public FloatRingBuffer(int capacitySamples)
    {
        if (capacitySamples < 2) throw new ArgumentOutOfRangeException(nameof(capacitySamples));
        _buffer = new float[capacitySamples];
    }

    public long DroppedSamples => Interlocked.Read(ref _droppedSamples);

    public void Write(ReadOnlySpan<float> samples)
    {
        if (samples.IsEmpty) return;
        lock (_gate)
        {
            if (samples.Length >= _buffer.Length)
            {
                var queuedBeforeOverwrite = _write - _read;
                var skippedFromIncoming = samples.Length - _buffer.Length;
                samples = samples[skippedFromIncoming..];
                _read = _write;
                Interlocked.Add(ref _droppedSamples, queuedBeforeOverwrite + skippedFromIncoming);
            }

            var queued = _write - _read;
            var free = _buffer.Length - (int)queued;
            if (samples.Length > free)
            {
                var drop = samples.Length - free;
                _read += drop;
                Interlocked.Add(ref _droppedSamples, drop);
            }

            var offset = (int)(_write % _buffer.Length);
            var first = Math.Min(samples.Length, _buffer.Length - offset);
            samples[..first].CopyTo(_buffer.AsSpan(offset, first));
            if (first < samples.Length)
                samples[first..].CopyTo(_buffer.AsSpan(0, samples.Length - first));
            _write += samples.Length;
        }
    }

    /// <summary>Reads up to destination.Length samples, pads missing samples with silence.</summary>
    public int ReadPadded(Span<float> destination)
    {
        lock (_gate)
        {
            var available = (int)Math.Min(destination.Length, _write - _read);
            if (available > 0)
            {
                var offset = (int)(_read % _buffer.Length);
                var first = Math.Min(available, _buffer.Length - offset);
                _buffer.AsSpan(offset, first).CopyTo(destination[..first]);
                if (first < available)
                    _buffer.AsSpan(0, available - first).CopyTo(destination.Slice(first, available - first));
                _read += available;
            }
            destination[available..].Clear();
            return available;
        }
    }
}
