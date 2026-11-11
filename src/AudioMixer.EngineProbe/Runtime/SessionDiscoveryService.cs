using System.Collections.Concurrent;
using System.Diagnostics;
using AudioMixer.EngineProbe.Audio;
using AudioMixer.EngineProbe.Config;
using NAudio.CoreAudioApi;

namespace AudioMixer.EngineProbe.Runtime;

public sealed class SessionDiscoveryService : IAsyncDisposable
{
    private readonly EngineSettings _settings;
    private readonly MixerEngine _engine;
    private readonly MMDevice _ingressDevice;
    private readonly CancellationTokenSource _stop = new();
    private readonly ConcurrentDictionary<int, DateTimeOffset> _lastSeen = new();
    private Task? _task;

    public SessionDiscoveryService(EngineSettings settings, MixerEngine engine)
    {
        _settings = settings;
        _engine = engine;
        _ingressDevice = DeviceResolver.ResolveRenderDevice(settings.IngressDeviceContains, useDefaultIfEmpty: false);
    }

    public string IngressDeviceName => _ingressDevice.FriendlyName;
    public string IngressDeviceId => _ingressDevice.ID;

    public void Start() => _task = Task.Run(() => DiscoveryLoopAsync(_stop.Token));

    private async Task DiscoveryLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_settings.DiscoveryIntervalMilliseconds));
        await ScanOnceAsync(cancellationToken);
        while (await timer.WaitForNextTickAsync(cancellationToken))
            await ScanOnceAsync(cancellationToken);
    }

    private async Task ScanOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            var manager = _ingressDevice.AudioSessionManager;
            manager.RefreshSessions();
            var sessions = manager.Sessions;
            var now = DateTimeOffset.UtcNow;

            for (var i = 0; i < sessions.Count; i++)
            {
                using var session = sessions[i];
                try
                {
                    if (session.State == AudioSessionState.Expired) continue;
                    var pid = checked((int)session.GetProcessID);
                    if (pid <= 0 || pid == Environment.ProcessId) continue;

                    string processName;
                    try
                    {
                        using var process = Process.GetProcessById(pid);
                        processName = process.ProcessName;
                    }
                    catch (ArgumentException)
                    {
                        continue; // Process exited between Core Audio enumeration and lookup.
                    }

                    _lastSeen[pid] = now;
                    var existing = _engine.Sources.FirstOrDefault(s => s.ProcessId == pid);
                    if (existing is not null)
                    {
                        if (existing.LastError is null) continue;
                        Console.WriteLine($"[RECOVERY] PID {pid} capture failed: {existing.LastError}. Restarting.");
                        await _engine.RemoveProcessAsync(pid);
                    }
                    var channel = ResolveChannel(processName);
                    try
                    {
                        await _engine.AddProcessAsync(pid, processName, channel, cancellationToken);
                        Console.WriteLine($"[CAPTURE] PID {pid} {processName} -> {channel}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[WARN] Cannot start process loopback for PID {pid} ({processName}): {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WARN] Session read failed: {ex.Message}");
                }
            }

            foreach (var entry in _lastSeen.ToArray())
            {
                if ((now - entry.Value).TotalSeconds <= _settings.StaleProcessSeconds) continue;
                _lastSeen.TryRemove(entry.Key, out _);
                await _engine.RemoveProcessAsync(entry.Key);
                Console.WriteLine($"[CAPTURE] PID {entry.Key} removed (session expired)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DISCOVERY ERROR] {ex.Message}");
        }
    }

    private string ResolveChannel(string processName)
    {
        if (_settings.ApplicationChannels.TryGetValue(processName, out var configured)
            && MixerEngine.IsChannel(configured)) return configured.ToUpperInvariant();
        return "AUX";
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        if (_task is not null)
        {
            try { await _task; } catch (OperationCanceledException) { }
        }
        _ingressDevice.Dispose();
        _stop.Dispose();
    }
}
