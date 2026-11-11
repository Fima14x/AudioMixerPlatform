using System.Runtime.InteropServices;
using NAudio.Wave;
using NAudio.CoreAudioApi;

namespace AudioMixer.EngineProbe.Audio;

public sealed class ProcessAudioSource : IAsyncDisposable
{
    private readonly WasapiRecorder _recorder;
    private readonly float[] _mixScratch;
    private int _disposed;
    private volatile string? _lastError;

    private ProcessAudioSource(int processId, string processName, string channel, WasapiRecorder recorder, int blockSamples, int ringCapacitySamples)
    {
        ProcessId = processId;
        ProcessName = processName;
        Channel = channel;
        _recorder = recorder;
        _mixScratch = new float[blockSamples];
        Samples = new FloatRingBuffer(ringCapacitySamples);
        _recorder.DataAvailable += OnDataAvailable;
        _recorder.RecordingStopped += OnRecordingStopped;
    }

    public int ProcessId { get; }
    public string ProcessName { get; }
    public string Channel { get; set; }
    public FloatRingBuffer Samples { get; }
    public Span<float> Scratch => _mixScratch;
    public string? LastError => _lastError;

    public static async Task<ProcessAudioSource> StartAsync(
        int processId,
        string processName,
        string channel,
        int sampleRate,
        int channels,
        int blockSamples,
        int ringCapacitySamples,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var format = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
        var recorder = await new WasapiRecorderBuilder()
            .WithProcessLoopback((uint)processId, ProcessLoopbackMode.IncludeTargetProcessTree)
            .WithFormat(format)
            .WithBufferLength(50)
            .WithMmcssThreadPriority("Pro Audio")
            .BuildAsync();

        var source = new ProcessAudioSource(processId, processName, channel, recorder, blockSamples, ringCapacitySamples);
        try
        {
            recorder.StartRecording();
            return source;
        }
        catch
        {
            await source.DisposeAsync();
            throw;
        }
    }

    private void OnDataAvailable(ReadOnlySpan<byte> buffer, AudioClientBufferFlags flags, long devicePosition, long qpcPosition)
    {
        if ((flags & AudioClientBufferFlags.Silent) != 0)
            return;

        // Process Loopback is explicitly configured as 32-bit IEEE float PCM.
        var samples = MemoryMarshal.Cast<byte, float>(buffer);
        Samples.Write(samples);
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null)
            _lastError = e.Exception.Message;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _recorder.DataAvailable -= OnDataAvailable;
        _recorder.RecordingStopped -= OnRecordingStopped;
        try { _recorder.StopRecording(); } catch { /* The stream may already have stopped. */ }
        await _recorder.DisposeAsync();
    }
}
