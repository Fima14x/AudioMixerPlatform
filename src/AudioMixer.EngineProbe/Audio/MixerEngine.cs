using System.Collections.Concurrent;
using AudioMixer.EngineProbe.Config;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace AudioMixer.EngineProbe.Audio;

public sealed class MixerEngine : IAsyncDisposable
{
    private readonly EngineSettings _settings;
    private readonly MMDevice _personalDevice;
    private readonly MMDevice _streamDevice;
    private readonly FloatOutputProvider _personalBuffer;
    private readonly FloatOutputProvider _streamBuffer;
    private readonly WasapiPlayer _personalPlayer;
    private readonly WasapiPlayer _streamPlayer;
    private readonly ConcurrentDictionary<int, ProcessAudioSource> _sources = new();
    private ProcessAudioSource[] _sourceSnapshot = Array.Empty<ProcessAudioSource>();
    private readonly float[] _personalMix;
    private readonly float[] _streamMix;
    private readonly CancellationTokenSource _stop = new();
    private Task? _mixTask;
    private float _personalPeak;
    private float _streamPeak;
    private volatile string? _personalOutputError;
    private volatile string? _streamOutputError;

    public MixerEngine(EngineSettings settings)
    {
        _settings = settings;
        _personalDevice = DeviceResolver.ResolveRenderDevice(settings.PersonalOutputContains, useDefaultIfEmpty: true);
        _streamDevice = DeviceResolver.ResolveRenderDevice(settings.StreamOutputContains, useDefaultIfEmpty: false);
        if (string.Equals(_personalDevice.ID, _streamDevice.ID, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Personal и Stream должны выводиться в разные аудиоустройства.");

        var format = WaveFormat.CreateIeeeFloatWaveFormat(settings.SampleRate, settings.AudioChannels);
        var blockFrames = settings.SampleRate * settings.BlockMilliseconds / 1000;
        var blockSamples = blockFrames * settings.AudioChannels;
        _personalMix = new float[blockSamples];
        _streamMix = new float[blockSamples];
        _personalBuffer = new FloatOutputProvider(format, TimeSpan.FromMilliseconds(250));
        _streamBuffer = new FloatOutputProvider(format, TimeSpan.FromMilliseconds(250));
        _personalPlayer = new WasapiPlayerBuilder()
            .WithDevice(_personalDevice)
            .WithLatency(40)
            .WithMmcssThreadPriority("Pro Audio")
            .Build();
        _streamPlayer = new WasapiPlayerBuilder()
            .WithDevice(_streamDevice)
            .WithLatency(40)
            .WithMmcssThreadPriority("Pro Audio")
            .Build();
        _personalPlayer.Init(_personalBuffer);
        _streamPlayer.Init(_streamBuffer);
        _personalPlayer.PlaybackStopped += (_, e) => { if (e.Exception is not null) _personalOutputError = e.Exception.Message; };
        _streamPlayer.PlaybackStopped += (_, e) => { if (e.Exception is not null) _streamOutputError = e.Exception.Message; };
    }

    public string PersonalDeviceName => _personalDevice.FriendlyName;
    public string StreamDeviceName => _streamDevice.FriendlyName;
    public string PersonalDeviceId => _personalDevice.ID;
    public string StreamDeviceId => _streamDevice.ID;
    public IReadOnlyCollection<ProcessAudioSource> Sources => Volatile.Read(ref _sourceSnapshot);
    public long TotalDroppedSamples => Volatile.Read(ref _sourceSnapshot).Sum(s => s.Samples.DroppedSamples)
        + _personalBuffer.DroppedSamples + _streamBuffer.DroppedSamples;
    public float PersonalPeak => Volatile.Read(ref _personalPeak);
    public float StreamPeak => Volatile.Read(ref _streamPeak);

    public void Start()
    {
        _personalPlayer.Play();
        _streamPlayer.Play();
        _mixTask = Task.Run(() => MixLoopAsync(_stop.Token));
    }

    public async Task<bool> AddProcessAsync(int processId, string processName, string channel, CancellationToken cancellationToken = default)
    {
        if (_sources.ContainsKey(processId)) return false;
        var blockSamples = _settings.SampleRate * _settings.BlockMilliseconds / 1000 * _settings.AudioChannels;
        var ringSamples = _settings.SampleRate * _settings.AudioChannels / 2; // 500 ms per process.
        var source = await ProcessAudioSource.StartAsync(
            processId, processName, channel, _settings.SampleRate, _settings.AudioChannels,
            blockSamples, ringSamples, cancellationToken);
        if (_sources.TryAdd(processId, source))
        {
            RefreshSourceSnapshot();
            return true;
        }
        await source.DisposeAsync();
        return false;
    }

    public async Task RemoveProcessAsync(int processId)
    {
        if (_sources.TryRemove(processId, out var source))
        {
            RefreshSourceSnapshot();
            await source.DisposeAsync();
        }
    }

    public bool MoveProcess(int processId, string channel)
    {
        if (!IsChannel(channel) || !_sources.TryGetValue(processId, out var source)) return false;
        source.Channel = channel.ToUpperInvariant();
        _settings.ApplicationChannels[source.ProcessName] = source.Channel;
        return true;
    }

    public void SetChannelGain(string channel, string mix, float percentage)
    {
        var settings = GetChannel(channel);
        var gain = Math.Clamp(percentage, 0, 150) / 100f;
        if (mix.Equals("personal", StringComparison.OrdinalIgnoreCase)) settings.PersonalGain = gain;
        else if (mix.Equals("stream", StringComparison.OrdinalIgnoreCase)) settings.StreamGain = gain;
        else throw new ArgumentException("Mix must be personal or stream.", nameof(mix));
    }

    public void SetChannelMute(string channel, string mix, bool muted)
    {
        var settings = GetChannel(channel);
        if (mix.Equals("personal", StringComparison.OrdinalIgnoreCase)) settings.PersonalMute = muted;
        else if (mix.Equals("stream", StringComparison.OrdinalIgnoreCase)) settings.StreamMute = muted;
        else throw new ArgumentException("Mix must be personal or stream.", nameof(mix));
    }

    public void SetMasterGain(string mix, float percentage)
    {
        var gain = Math.Clamp(percentage, 0, 150) / 100f;
        if (mix.Equals("personal", StringComparison.OrdinalIgnoreCase)) _settings.Personal.Gain = gain;
        else if (mix.Equals("stream", StringComparison.OrdinalIgnoreCase)) _settings.Stream.Gain = gain;
        else throw new ArgumentException("Mix must be personal or stream.", nameof(mix));
    }

    public void SetMasterMute(string mix, bool muted)
    {
        if (mix.Equals("personal", StringComparison.OrdinalIgnoreCase)) _settings.Personal.Mute = muted;
        else if (mix.Equals("stream", StringComparison.OrdinalIgnoreCase)) _settings.Stream.Mute = muted;
        else throw new ArgumentException("Mix must be personal or stream.", nameof(mix));
    }

    public object GetSnapshot()
    {
        var settings = _settings;
        return new
        {
            schemaVersion = 1,
            capturedAt = DateTimeOffset.UtcNow,
            outputs = new
            {
                personal = new { device = PersonalDeviceName, gain = settings.Personal.Gain * 100f, muted = settings.Personal.Mute, peak = PersonalPeak, error = _personalOutputError },
                stream = new { device = StreamDeviceName, gain = settings.Stream.Gain * 100f, muted = settings.Stream.Mute, peak = StreamPeak, error = _streamOutputError }
            },
            channels = settings.ChannelMixes.Select(pair => new
            {
                id = pair.Key,
                personalGain = pair.Value.PersonalGain * 100f,
                streamGain = pair.Value.StreamGain * 100f,
                personalMuted = pair.Value.PersonalMute,
                streamMuted = pair.Value.StreamMute
            }).ToArray(),
            sources = Volatile.Read(ref _sourceSnapshot).Select(source => new
            {
                processId = source.ProcessId,
                processName = source.ProcessName,
                channel = source.Channel,
                active = source.LastError is null,
                error = source.LastError
            }).ToArray(),
            routingMode = "manual-windows-endpoint",
            capabilities = new[] { "process-loopback-capture", "dual-mix", "personal-render", "stream-render" }
        };
    }

    public string GetStatus()
    {
        var lines = new List<string>
        {
            $"PERSONAL: {_personalDevice.FriendlyName}, gain={_settings.Personal.Gain:P0}, mute={_settings.Personal.Mute}",
            $"STREAM:   {_streamDevice.FriendlyName}, gain={_settings.Stream.Gain:P0}, mute={_settings.Stream.Mute}",
            $"Sources: {_sources.Count}; dropped samples (capture + output queues): {TotalDroppedSamples}; PERSONAL peak={PersonalPeak:0.000}; STREAM peak={StreamPeak:0.000}",
            $"Output errors: PERSONAL={_personalOutputError ?? "none"}; STREAM={_streamOutputError ?? "none"}"
        };
        lines.AddRange(_sources.Values.OrderBy(s => s.Channel).ThenBy(s => s.ProcessName)
            .Select(s => $"  PID {s.ProcessId,-7} {s.ProcessName,-22} -> {s.Channel}  ringDrops={s.Samples.DroppedSamples}"));
        return string.Join(Environment.NewLine, lines);
    }

    private async Task MixLoopAsync(CancellationToken cancellationToken)
    {
        var block = TimeSpan.FromMilliseconds(_settings.BlockMilliseconds);
        using var timer = new PeriodicTimer(block);
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            Array.Clear(_personalMix);
            Array.Clear(_streamMix);

            foreach (var source in Volatile.Read(ref _sourceSnapshot))
            {
                var available = source.Samples.ReadPadded(source.Scratch);
                _ = available; // Silence padding keeps every source block aligned to the engine tick.
                if (!_settings.ChannelMixes.TryGetValue(source.Channel, out var channel))
                    continue;

                var personalGain = _settings.Personal.Mute || channel.PersonalMute
                    ? 0f : _settings.Personal.Gain * channel.PersonalGain;
                var streamGain = _settings.Stream.Mute || channel.StreamMute
                    ? 0f : _settings.Stream.Gain * channel.StreamGain;

                var samples = source.Scratch;
                for (var i = 0; i < samples.Length; i++)
                {
                    var sample = samples[i];
                    _personalMix[i] += sample * personalGain;
                    _streamMix[i] += sample * streamGain;
                }
            }

            ApplySoftClip(_personalMix);
            ApplySoftClip(_streamMix);
            Volatile.Write(ref _personalPeak, Peak(_personalMix));
            Volatile.Write(ref _streamPeak, Peak(_streamMix));
            _personalBuffer.AddSamples(_personalMix);
            _streamBuffer.AddSamples(_streamMix);
        }
    }

    private void RefreshSourceSnapshot() => Volatile.Write(ref _sourceSnapshot, _sources.Values.ToArray());

    private static float Peak(ReadOnlySpan<float> block)
    {
        var peak = 0f;
        for (var i = 0; i < block.Length; i++) peak = Math.Max(peak, Math.Abs(block[i]));
        return peak;
    }

    // A memoryless safety soft clip, not a broadcast-grade look-ahead limiter.
    // It is continuous at the knee and asymptotically approaches 0.98 full scale.
    private static void ApplySoftClip(Span<float> block)
    {
        const float knee = 0.95f;
        const float ceiling = 0.98f;
        const float width = ceiling - knee;
        for (var i = 0; i < block.Length; i++)
        {
            var sample = block[i];
            var magnitude = Math.Abs(sample);
            if (magnitude > knee)
            {
                var clipped = knee + width * (1f - MathF.Exp(-(magnitude - knee) / width));
                block[i] = MathF.CopySign(clipped, sample);
            }
        }
    }

    private ChannelSettings GetChannel(string channel)
    {
        if (!_settings.ChannelMixes.TryGetValue(channel, out var settings))
            throw new ArgumentException($"Unknown channel '{channel}'.", nameof(channel));
        return settings;
    }

    public static bool IsChannel(string channel) => channel.Equals("GAME", StringComparison.OrdinalIgnoreCase)
        || channel.Equals("CHAT", StringComparison.OrdinalIgnoreCase)
        || channel.Equals("MEDIA", StringComparison.OrdinalIgnoreCase)
        || channel.Equals("AUX", StringComparison.OrdinalIgnoreCase);

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        if (_mixTask is not null)
        {
            try { await _mixTask; } catch (OperationCanceledException) { }
        }
        foreach (var source in Volatile.Read(ref _sourceSnapshot))
            await source.DisposeAsync();
        _sources.Clear();
        RefreshSourceSnapshot();
        _personalPlayer.Stop();
        _streamPlayer.Stop();
        _personalPlayer.Dispose();
        _streamPlayer.Dispose();
        _personalDevice.Dispose();
        _streamDevice.Dispose();
        _stop.Dispose();
    }
}
