using System.Runtime.InteropServices;
using NAudio.Wave;

namespace AudioMixer.EngineProbe.Audio;

/// <summary>
/// NAudio 3-compatible float output provider with a bounded queue.
/// Read is called by WASAPI's render thread; AddSamples is called by the mixer thread.
/// </summary>
public sealed class FloatOutputProvider : IWaveProvider
{
    private readonly FloatRingBuffer _samples;

    public FloatOutputProvider(WaveFormat format, TimeSpan capacity)
    {
        if (format.Encoding != WaveFormatEncoding.IeeeFloat || format.BitsPerSample != 32)
            throw new ArgumentException("FloatOutputProvider requires 32-bit IEEE float format.", nameof(format));
        if (capacity <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        WaveFormat = format;
        var sampleCapacity = checked((int)Math.Ceiling(format.SampleRate * format.Channels * capacity.TotalSeconds));
        _samples = new FloatRingBuffer(Math.Max(sampleCapacity, format.Channels * 2));
    }

    public WaveFormat WaveFormat { get; }
    public long DroppedSamples => _samples.DroppedSamples;

    public void AddSamples(ReadOnlySpan<float> samples) => _samples.Write(samples);

    public int Read(Span<byte> destination)
    {
        var alignedBytes = destination.Length - destination.Length % sizeof(float);
        var floatDestination = MemoryMarshal.Cast<byte, float>(destination[..alignedBytes]);
        _samples.ReadPadded(floatDestination);
        destination[alignedBytes..].Clear();
        return destination.Length;
    }
}
