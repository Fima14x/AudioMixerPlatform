# Technical research and capability matrix

Research snapshot: 11 November 2026. Target workstation baseline is Windows 11 build 26100; project build target is .NET 10 LTS.

## 1. Windows capture options

| API / method | What it can do | What it cannot do | Decision |
|---|---|---|---|
| `IAudioSessionManager2` / session enumeration | Detect sessions on a render endpoint, obtain PID/session identity, query state, session volume/mute/meter | It does not extract an arbitrary app's PCM and does not move a process into a custom virtual bus | Use for app discovery and metadata only |
| WASAPI endpoint loopback | Capture the mixed output of one render endpoint | Cannot distinguish apps after Windows has mixed them | Useful for a single endpoint recorder, not this per-app mixer |
| Process Loopback (`ActivateAudioInterfaceAsync`, `AUDIOCLIENT_ACTIVATION_TYPE_PROCESS_LOOPBACK`) | Capture the rendered audio for a PID and optionally its child-process tree | Does not mute or reroute the original process; it is a capture tap, not a virtual cable; capture must be coordinated with process-tree ownership | Use for per-process PCM in the prototype |
| WASAPI render | Play each engine mix to a selected endpoint in shared mode | Does not create new system endpoints | Use for physical Personal and the virtual Stream render endpoint |
| Windows per-app Volume Mixer | Assign an app to a chosen render endpoint | Does not provide an app's samples to our DSP engine | Manual ingress routing for stage 1 |
| Own virtual audio driver | Can expose stable virtual render/capture endpoints and support transparent system-level integration | Requires WDK work, signing, compatibility and separate installation/testing | Later only if third-party virtual endpoints are insufficient |

Microsoft documents Process Loopback as a mode of `ActivateAudioInterfaceAsync` that captures a target process and optionally its child processes. The Microsoft activation-type page lists Windows 10 build 20348 as its conservative minimum. NAudio's current Process Loopback builder exposes `WithProcessLoopback(uint, ProcessLoopbackMode)` and async `BuildAsync()`; its own code is annotated for Windows 10 build 19041. This project chooses build 20348 as the conservative documented floor. User's Windows 11 build 26100 exceeds both.

NAudio 3.3.0 provides `WasapiRecorderBuilder` / `WasapiRecorder` for process loopback and `WasapiPlayerBuilder` / `WasapiPlayer` for render. `NAudio.Wasapi` 3.3.0 targets .NET 9 and NuGet declares compatibility with that framework or higher, so it can be consumed by the project's .NET 10 target. The package is MIT licensed.

Official references:

- Microsoft Process Loopback activation type: https://learn.microsoft.com/en-us/windows/win32/api/audioclientactivationparams/ne-audioclientactivationparams-audioclient_activation_type
- Microsoft Application Loopback sample: https://github.com/microsoft/Windows-classic-samples/tree/main/Samples/ApplicationLoopback
- NAudio WasapiRecorder docs: https://naudio.github.io/NAudio/docs/WasapiRecorder.html
- NAudio WasapiPlayer docs: https://naudio.github.io/NAudio/docs/WasapiPlayer.html
- NAudio.Wasapi 3.3.0 NuGet: https://www.nuget.org/packages/NAudio.Wasapi/3.3.0
- .NET lifecycle policy: https://dotnet.microsoft.com/en-us/platform/support/policy

## 2. Why `IAudioSessionManager2` is not enough

`IAudioSessionManager2` adds session enumeration and notification registration to the session manager. NAudio's `AudioSessionControl` exposes a process ID and `SimpleAudioVolume`; those APIs manage the Windows session's volume/mute. They are not a PCM capture interface and do not allow arbitrary redirection to a user-defined software channel. The implementation therefore uses session enumeration to discover PIDs and Process Loopback to acquire their PCM independently.

## 3. How two mixes work

For every source block `x[n]`, channel state has separate scalar gains and mute bits:

```text
personal[n] += x[n] * channel.personalGain * master.personalGain
stream[n]   += x[n] * channel.streamGain   * master.streamGain
```

Each mix has its own mute and a separate WASAPI playback stream. The two output streams are not copies of each other. The prototype supports channel gains up to 150% and applies a fixed memoryless soft-clip safety curve to keep samples below full scale (not a configurable/look-ahead limiter). A real compressor/limiter with envelope detection and configurable attack/release is a later DSP stage.

## 4. Virtual audio endpoints

### Stage 1 minimum

- `VB-CABLE Input` playback/render endpoint: apps are assigned here from Windows Volume Mixer; Core Audio sessions on this endpoint give the process list.
- `VB-CABLE A Input` playback/render endpoint: receives Stream Mix.
- `VB-CABLE A Output` recording/capture endpoint: selected in OBS as the Stream Mix source.
- Physical headphones/speakers: Personal Mix.

VB-Audio describes its base cable as a virtual connection from its playback/input side to its recording/output side. The A+B package supplies two additional independent cables; VB-Audio calls the extra cables donationware. We do not bundle driver binaries and do not redistribute its license.

Vendor: https://vb-audio.com/Cable/VirtualCables.htm

### Future microphone path

A future processed microphone send needs a *separate* virtual cable: physical microphone → microphone capture/DSP → render to `CABLE B Input` → Discord chooses `CABLE B Output` as its microphone. Do not reuse the game ingress cable for the processed mic return, otherwise application audio and mic return could be combined in an unintended loop.

### Driver implications

A virtual endpoint is a Windows audio driver. A pure C# process cannot register a new Core Audio render/capture endpoint with no driver. Using VB-CABLE avoids writing a driver for the initial prototype. If a custom driver is eventually required, it becomes a separate WDK project with proper catalog signing, Secure Boot compatibility, install/uninstall tests, and independent release lifecycle. No test-signing or unsigned-driver shortcut is acceptable for daily use.

## 5. Platform capability matrix

| Feature | Public user-mode API | Virtual driver required? | Notes |
|---|---|---|---|
| Enumerate active audio sessions | Yes | No | Core Audio session manager |
| Get PID/name and session state | Yes | No | Session metadata plus process APIs |
| Capture one process tree | Yes, supported OS build | No | Process Loopback |
| Independently change engine gains | Yes | No | User-mode float mixer |
| Output PERSONAL to physical device | Yes | No | WASAPI shared render |
| Expose STREAM to OBS as a selectable endpoint | No, not as a new endpoint | Yes, or use a supported virtual cable | Use VB-CABLE A |
| Transparent routing of every application without per-app setup | Not with session API alone | Usually yes | Use a virtual render endpoint / driver integration |
| Processed microphone as a selectable input for Discord | No, not as a new input endpoint | Yes, or use supported virtual cable | Use a separate cable for mic return |
| EQ/compressor/gate/noise reduction | Yes | No | DSP in engine; quality depends on implementation |
| Control app volume in Windows mixer | Yes | No | Does not replace engine channel gains |

## 6. Synchronization and sample formats

The probe requests 48 kHz, stereo, 32-bit IEEE float from every process-loopback virtual endpoint. It avoids per-source format changes at this stage. Each render endpoint uses WASAPI shared mode; Windows may convert the engine's 48 kHz float stream to the endpoint mix format. This is safer for mixed hardware than forcing exclusive mode.

The current prototype has independent render clocks and no adaptive resampling. It must pass a 1-hour drift/underrun test before being considered reliable. The next audio milestone is to use output clock/padding measurements and a lightweight adaptive resampler per output queue, or a single synchronized hardware endpoint where possible.
