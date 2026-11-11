using System.Text.Json;
using System.Text.Json.Serialization;

namespace AudioMixer.EngineProbe.Config;

public sealed class EngineSettings
{
    public string IngressDeviceContains { get; set; } = "CABLE Input";
    public string PersonalOutputContains { get; set; } = ""; // Empty = Windows Multimedia default render device.
    public string StreamOutputContains { get; set; } = "CABLE-A Input";
    public int SampleRate { get; set; } = 48_000;
    public int AudioChannels { get; set; } = 2;
    public int BlockMilliseconds { get; set; } = 10;
    public int DiscoveryIntervalMilliseconds { get; set; } = 900;
    public int StaleProcessSeconds { get; set; } = 5;
    public Dictionary<string, string> ApplicationChannels { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cs2"] = "GAME",
        ["discord"] = "CHAT",
        ["teamspeak"] = "CHAT",
        ["spotify"] = "MEDIA",
        ["vlc"] = "MEDIA",
        ["chrome"] = "AUX",
        ["msedge"] = "AUX",
        ["firefox"] = "AUX"
    };
    public Dictionary<string, ChannelSettings> ChannelMixes { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GAME"] = new(), ["CHAT"] = new(), ["MEDIA"] = new(), ["AUX"] = new()
    };
    public MixSettings Personal { get; set; } = new();
    public MixSettings Stream { get; set; } = new();

    public void Validate()
    {
        if (SampleRate != 48_000) throw new InvalidOperationException("Stage 1 currently supports 48000 Hz only.");
        if (AudioChannels != 2) throw new InvalidOperationException("Stage 1 currently supports stereo only.");
        if (BlockMilliseconds < 5 || BlockMilliseconds > 20 || 1000 % BlockMilliseconds != 0)
            throw new InvalidOperationException("BlockMilliseconds must be 5, 10, or 20 for Stage 1.");
        if (DiscoveryIntervalMilliseconds < 250 || DiscoveryIntervalMilliseconds > 5000)
            throw new InvalidOperationException("DiscoveryIntervalMilliseconds must be between 250 and 5000.");
        if (StaleProcessSeconds < 2 || StaleProcessSeconds > 60)
            throw new InvalidOperationException("StaleProcessSeconds must be between 2 and 60.");
        foreach (var required in new[] { "GAME", "CHAT", "MEDIA", "AUX" })
            ChannelMixes.TryAdd(required, new ChannelSettings());
        Personal ??= new MixSettings();
        Stream ??= new MixSettings();
    }

    public static async Task<EngineSettings> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            var defaults = new EngineSettings();
            await defaults.SaveAsync(path, cancellationToken);
            return defaults;
        }

        await using var stream = File.OpenRead(path);
        var loaded = await JsonSerializer.DeserializeAsync<EngineSettings>(stream, JsonOptions, cancellationToken);
        loaded ??= new EngineSettings();
        loaded.ApplicationChannels = new Dictionary<string, string>(loaded.ApplicationChannels ?? new(), StringComparer.OrdinalIgnoreCase);
        loaded.ChannelMixes = new Dictionary<string, ChannelSettings>(loaded.ChannelMixes ?? new(), StringComparer.OrdinalIgnoreCase);
        return loaded;
    }

    public async Task SaveAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporaryPath = fullPath + ".tmp";
        await using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 16_384, useAsync: true))
        {
            await JsonSerializer.SerializeAsync(stream, this, JsonOptions, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
        File.Move(temporaryPath, fullPath, overwrite: true);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };
}

public sealed class ChannelSettings
{
    public float PersonalGain { get; set; } = 1.0f;
    public float StreamGain { get; set; } = 1.0f;
    public bool PersonalMute { get; set; }
    public bool StreamMute { get; set; }
}

public sealed class MixSettings
{
    public float Gain { get; set; } = 1.0f;
    public bool Mute { get; set; }
}
