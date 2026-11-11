# Engine Probe validation plan

## Prerequisites

- Windows 11 build 26100 or another build at/above the documented Process Loopback floor.
- .NET 10 runtime.
- VB-CABLE base cable and VB-CABLE A+B package, installed from VB-Audio.
- Headphones/speakers and OBS.
- Two or more audio-producing apps (for example a game, Discord test call, and browser video).

## Pre-flight safety

1. Do not change the Windows-wide default output during the first test.
2. Route only one test app to `CABLE Input` via Windows Volume Mixer.
3. Confirm that app is not also sent directly to the physical headphones by another audio path.
4. Configure `CABLE-A Input` for STREAM and `CABLE-A Output` in OBS.
5. Keep a second device or a clear route back to the original headphones available.

## Functional matrix

| ID | Test | Expected result | Record |
|---|---|---|---|
| E01 | Start one app routed to ingress cable | PID appears within two discovery intervals | PID, app, delay |
| E02 | Start second app | Both PIDs listed independently | PID count |
| E03 | Play only app A | Personal and Stream meters/outputs respond; app B remains silent | subjective check + OBS meter |
| E04 | Set GAME personal gain 0, stream 100 | Personal game audio silent; OBS stream game audio remains | pass/fail |
| E05 | Set GAME stream mute on | OBS game audio silent; personal remains | pass/fail |
| E06 | Move PID GAME → AUX | New channel gain/mute controls affect only that source | pass/fail |
| E07 | Quit an app and restart it | Old PID is removed; new PID returns using saved executable route | PIDs, route |
| E08 | Close the engine | App is still assigned to cable; switch back to physical output and verify audio restored | pass/fail |
| E09 | Run game + Discord + browser | No duplicate source audio; all app routes separate | pass/fail |
| E10 | OBS capture | OBS gets Stream only, not Personal | pass/fail |

## Stability / performance matrix

| ID | Test | Acceptance target |
|---|---|---|
| S01 | 60-minute mixed playback | No persistent underrun, buffer overflow, or accumulating delay |
| S02 | Two outputs active | No progressive queue growth; if drift appears, log it and implement ASRC before release |
| S03 | App start/exit repeatedly | No orphan capture task or PID reuse bug |
| S04 | Unplug/replug Personal output | Engine logs endpoint invalidation; current prototype may require restart — do not call this a pass for production |
| S05 | CPU / RAM during 3 sources | Record baseline and peak; check no unbounded growth |
| S06 | High mix gain | No digital wraparound; soft safety clip keeps samples bounded |
| S07 | 8-hour soak | No memory leak, crash, or unbounded audio buffer |

## Current known expected gaps

- The current Engine Probe is expected to need a full restart if a selected output device is unplugged.
- It does not yet perform adaptive clock-drift correction between independent output endpoints.
- It does not yet provide true real-time peak meters to a UI.
- It does not yet perform DSP beyond fixed soft clipping.
- Process-tree overlap must be tested with applications that spawn multiple child renderer processes.

Do not advance to Stage 2 until E01–E10 pass and S01–S06 are understood. Stage 1 is not accepted based solely on a successful build.
