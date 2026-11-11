# Android ↔ Windows transport decision

## One transport-independent protocol

The Host exposes one internal command/event interface. Transport adapters only move framed messages; audio does not travel to the phone.

```text
Android UI
   │
   ├── Wi-Fi adapter ───── WebSocket over TLS / authenticated local session
   │
   └── USB adapter ─────── WebSocket over USB-tether network interface (initial strategy)
                              │
                       Windows Host API
                              │
                       Shared State Store
                              │
                         Audio Engine
```

Message envelope (draft):

```json
{
  "protocol": 1,
  "kind": "command",
  "commandId": "client-generated-uuid",
  "expectedRevision": 43,
  "name": "SetChannelGain",
  "payload": { "channelId": "GAME", "mix": "personal", "gain": 0.72 }
}
```

Host replies with accepted/rejected result and a monotonically increasing state revision. It broadcasts compact `state.delta` messages to every connected UI. Reconnecting clients first receive a full snapshot and then deltas. Duplicate command IDs are idempotent. Commands that conflict with a newer revision are rejected or revalidated, never silently overwrite the newest state.

## Wi-Fi

- Windows Host advertises itself on the private LAN using mDNS; QR pairing contains an ephemeral pairing token and host identifier, not a permanent password.
- Pairing is user-approved on both ends.
- WebSocket connection uses TLS with a pinned per-install certificate or a one-time pairing key that derives a session key.
- Host binds only to private interfaces; no internet listener and no port-forwarding requirement.
- Manual IP entry remains available for routers that block multicast discovery.

## USB: prefer USB tethering before a custom USB accessory protocol

Android supports both USB Host and USB Accessory modes. In Accessory mode the *external device* (the Windows-side accessory implementation) is USB host and Android is the accessory; this requires the Windows side to implement the Android Open Accessory protocol and the Android app to request the accessory interface. It is not equivalent to “plug any Android phone into a PC and open a serial port”. USB Host mode reverses the roles and is not the normal PC-to-phone cable arrangement.

For a practical no-ADB USB path, first implement USB tethering as a network link. Android's user-facing USB tethering feature creates a network interface on the PC; then the same authenticated WebSocket protocol runs over that point-to-point interface. The user toggles USB tethering on the phone. This avoids requiring USB Debugging, a custom kernel driver, or bespoke bulk endpoint plumbing. It may require Windows to finish installing its built-in network adapter driver and may depend on device/OEM behavior.

The direct Android Accessory API remains a future adapter option, but only after verifying that the POCO firmware and Windows-side host implementation work reliably. It should not be the first development blocker.

Official references:

- Android USB accessory: https://developer.android.com/develop/connectivity/usb/accessory
- Android USB host: https://developer.android.com/develop/connectivity/usb/host
- Android Help — USB tethering steps: https://support.google.com/android/answer/9059108

## Reconnection behavior

- Wi-Fi adapter continuously retries only while the user has enabled auto-reconnect.
- USB adapter detects the tether interface going up/down; the user may re-enable USB tethering if the phone disables it.
- Loss of either transport never stops the Windows audio engine.
- The phone UI shows stale/offline state if a heartbeat is missed; controls are disabled or queued only when safe.
- A reconnect always starts with a full Host snapshot before accepting further revision-sensitive commands.
