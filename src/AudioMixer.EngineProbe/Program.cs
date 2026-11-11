using AudioMixer.EngineProbe.Audio;
using AudioMixer.EngineProbe.Config;
using AudioMixer.EngineProbe.Runtime;

namespace AudioMixer.EngineProbe;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 20348))
        {
            Console.Error.WriteLine("Process Loopback requires Windows build 20348 or newer according to the Windows SDK documentation.");
            return 2;
        }

        var settingsPath = Path.Combine(AppContext.BaseDirectory, "settings.json");
        try
        {
            var settings = await EngineSettings.LoadAsync(settingsPath);
            settings.Validate();
            await using var engine = new MixerEngine(settings);
            await using var discovery = new SessionDiscoveryService(settings, engine);
            var endpointIds = new[] { engine.PersonalDeviceId, engine.StreamDeviceId, discovery.IngressDeviceId };
            if (endpointIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != endpointIds.Length)
                throw new InvalidOperationException("Ingress, Personal и Stream должны быть тремя разными Windows endpoints.");
            engine.Start();
            discovery.Start();

            Console.WriteLine("AudioMixer Engine Probe — stage 1");
            Console.WriteLine($"Ingress:  {discovery.IngressDeviceName}");
            Console.WriteLine($"Personal: {engine.PersonalDeviceName}");
            Console.WriteLine($"Stream:   {engine.StreamDeviceName}");
            Console.WriteLine("Type 'help' for commands. Use 'quit' to save settings and exit cleanly.");

            while (true)
            {
                Console.Write("mixer> ");
                var line = Console.ReadLine();
                if (line is null) break;
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length == 0) continue;
                var shouldSave = false;
                try
                {
                    switch (parts[0].ToLowerInvariant())
                    {
                        case "help":
                            PrintHelp();
                            break;
                        case "list":
                            Console.WriteLine(engine.GetStatus());
                            break;
                        case "status":
                            Console.WriteLine(engine.GetStatus());
                            break;
                        case "route" when parts.Length == 3 && int.TryParse(parts[1], out var routePid):
                            if (!MixerEngine.IsChannel(parts[2])) throw new ArgumentException("Channel must be GAME, CHAT, MEDIA or AUX.");
                            if (!engine.MoveProcess(routePid, parts[2])) throw new InvalidOperationException($"PID {routePid} is not captured.");
                            Console.WriteLine($"PID {routePid} -> {parts[2].ToUpperInvariant()}");
                            shouldSave = true;
                            break;
                        case "gain" when parts.Length == 4 && float.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var gain):
                            engine.SetChannelGain(parts[1], parts[2], gain);
                            Console.WriteLine($"{parts[1]} {parts[2]} gain = {gain:0.#}%");
                            shouldSave = true;
                            break;
                        case "mute" when parts.Length == 4:
                            engine.SetChannelMute(parts[1], parts[2], ParseOnOff(parts[3]));
                            Console.WriteLine($"{parts[1]} {parts[2]} mute = {parts[3]}");
                            shouldSave = true;
                            break;
                        case "master" when parts.Length == 3 && float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var masterGain):
                            engine.SetMasterGain(parts[1], masterGain);
                            shouldSave = true;
                            break;
                        case "mastermute" when parts.Length == 3:
                            engine.SetMasterMute(parts[1], ParseOnOff(parts[2]));
                            shouldSave = true;
                            break;
                        case "save":
                            shouldSave = true;
                            break;
                        case "quit":
                        case "exit":
                            await settings.SaveAsync(settingsPath);
                            return 0;
                        default:
                            Console.WriteLine("Unknown command or invalid arguments. Type 'help'.");
                            break;
                    }

                    if (shouldSave)
                        await settings.SaveAsync(settingsPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[COMMAND ERROR] {ex.Message}");
                }
            }
            await settings.SaveAsync(settingsPath);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FATAL: {ex}");
            return 1;
        }
    }

    private static bool ParseOnOff(string value) => value.ToLowerInvariant() switch
    {
        "on" or "true" or "1" => true,
        "off" or "false" or "0" => false,
        _ => throw new ArgumentException("Use on or off.")
    };

    private static void PrintHelp()
    {
        Console.WriteLine("""
        Commands:
          help                                      Show this help
          list | status                            List captured processes and mixer state
          route <PID> <GAME|CHAT|MEDIA|AUX>         Move a process between channels
          gain <channel> <personal|stream> <0..150> Set independent channel gain in percent
          mute <channel> <personal|stream> <on|off> Set independent channel mute
          master <personal|stream> <0..150>         Set mix master gain
          mastermute <personal|stream> <on|off>     Mute one whole mix
          save                                      Save settings now
          quit                                      Save and exit
        """);
    }
}
