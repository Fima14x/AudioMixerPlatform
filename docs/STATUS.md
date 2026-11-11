# Project status — 11 November 2026

## Existing audio prototype

- Windows console Engine Probe targeting x64 / .NET 10 / Windows build 20348+.
- Core Audio session discovery on a selected virtual ingress render endpoint.
- Process Loopback capture per discovered PID, including its child process tree.
- Bounded per-process float ring buffers and separate PERSONAL / STREAM summing paths.
- Two WASAPI shared-mode output players and JSON settings persistence.
- A JSON-compatible snapshot shape has been added for future Host/UI integration; no network service consumes it yet.

## New UI scaffolds

- A Windows WPF preview shell exists under `src/AudioMixer.Desktop`.
- An Android Kotlin/Jetpack Compose preview shell exists under `mobile`.
- The sliders/switches in these previews are deliberately disabled or local-only. They are not connected to the engine and must not be presented as working audio controls.

## Build status

- GitHub Actions workflows are present for the Windows projects and Android APK.
- Cloud builds have not been run because no repository URL/access has been connected in this workspace.
- Local .NET runtimes are present but the .NET SDK is not installed. Android Studio/SDK is intentionally not installed to conserve disk.
- Compilation, package restore, and real-device audio playback are not verified.

## Important audio limitations

- App routing currently requires manual Windows per-app endpoint assignment to a virtual ingress endpoint. This is not transparent system-wide routing.
- The source has not been tested with real Windows endpoints or OBS.
- Process Loopback is a capture tap; it does not mute or reroute the original app. Confirm no double-playback in the target topology before daily use.
- There is no Android-to-Windows transport, authentication/pairing, working desktop control API, microphone path, DSP chain, profiles, adaptive clock drift compensation, endpoint hot-plug recovery, or production installer.

Do not call the UI a functional mixer until it is connected to a tested Host API and the audio acceptance tests pass.
