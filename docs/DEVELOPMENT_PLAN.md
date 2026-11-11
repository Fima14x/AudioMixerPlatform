# Development plan and stage gates

## Stage 1 — real audio engine proof

**Current work.** Build the console Engine Probe and validate:

1. Session discovery from the ingress virtual endpoint.
2. PID and process-name detection for multiple apps.
3. Separate Process Loopback capture per PID.
4. Two independent mixes with independent channel and master gain/mute.
5. Physical Personal output and virtual Stream output.
6. OBS can capture the Stream cable's recording endpoint.
7. Long-run output queue stability and no direct-source double playback.

Gate: a Windows test log plus repeatable test report; no GUI/Android milestone until these tests pass.

## Stage 2 — Windows interface

- WPF/WinUI selection after first build and UI performance proof.
- Desktop channel strips and app list; native drag/drop.
- Device selectors, meters, tray, window resizing, autostart.
- Host state remains independent of the UI.
- Device removal/recovery and source restart handling.

## Stage 3 — Android remote

- Kotlin + Jetpack Compose, portrait-first cards and 44–48 dp touch targets.
- Wi-Fi pairing/QR plus manual IP.
- Shared versioned command/event protocol.
- USB tethered-network adapter using the same protocol.
- Two-way state deltas and full-snapshot reconnect.

## Stage 4 — microphone and DSP

- Physical microphone capture, gain, monitor, PERSONAL and STREAM sends.
- Dedicated virtual mic return endpoint/cable for voice applications.
- Per-channel DSP chain with EQ biquads, compressor, limiter, gate/expander, high/low-pass.
- Noise reduction only after selecting and benchmarking an implementation; do not label a simple gate as noise reduction.
- Bypass per node, chain reorder, presets, CPU/latency metrics.

## Stage 5 — profiles and automation

- User profiles, executable-based automatic profile rules, per-game overrides.
- Hotkeys, user channels, colors, rename, profiles import/export.
- State schema versioning/migrations and robust settings backups.

## Stage 6 — production release

- Windows installer, app update channel, signed driver only if needed.
- Android APK and Play/internal distribution process.
- Full install/uninstall/rollback documentation.
- Compatibility matrix, latency/CPU/memory budgets, one-hour and overnight soak tests.

## Technology choices

- .NET 10 LTS for Host (current LTS on the research date); keep audio code isolated from UI.
- NAudio.Wasapi 3.3.0 to accelerate Core Audio/WASAPI integration; switch only if it blocks a needed OS feature or profiling justifies native code.
- Kotlin + Jetpack Compose for Android.
- WebSocket for a transport-independent remote protocol.
- GitHub Actions Windows runner for Windows artifact builds; separate Android workflow can be added without requiring local Android Studio.
- JSON atomic persistence in early stages; SQLite only when richer history or queryable profiles justify the dependency.
