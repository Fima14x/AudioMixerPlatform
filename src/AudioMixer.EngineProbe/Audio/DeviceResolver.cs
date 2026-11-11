using NAudio.CoreAudioApi;

namespace AudioMixer.EngineProbe.Audio;

public static class DeviceResolver
{
    public static MMDevice ResolveRenderDevice(string friendlyNameContains, bool useDefaultIfEmpty)
    {
        using var enumerator = new MMDeviceEnumerator();
        if (string.IsNullOrWhiteSpace(friendlyNameContains))
        {
            if (useDefaultIfEmpty)
                return enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            throw new InvalidOperationException("Для этого устройства необходимо задать непустой фрагмент имени в settings.json.");
        }

        using var endpoints = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        for (var i = 0; i < endpoints.Count; i++)
        {
            using var candidate = endpoints[i];
            if (candidate.FriendlyName.Contains(friendlyNameContains, StringComparison.OrdinalIgnoreCase))
                return enumerator.GetDevice(candidate.ID);
        }

        var names = new List<string>();
        using var allEndpoints = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        for (var i = 0; i < allEndpoints.Count; i++)
        {
            using var endpoint = allEndpoints[i];
            names.Add(endpoint.FriendlyName);
        }
        throw new InvalidOperationException(
            $"Не найдено устройство вывода, содержащее '{friendlyNameContains}'. Доступные устройства: {string.Join("; ", names)}");
    }
}
