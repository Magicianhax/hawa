# Hawa v1 design

Date: 2026-09-03
Status: approved design, pending implementation plan

## Goal

A Windows 11 tray application that gives AirPods Pro (2nd generation) the
iOS/macOS experience: a battery card when the case opens or the pods
connect, in-ear detection with automatic media pause and resume, a tray
icon with battery status, and a settings window. Noise control and other
Apple Accessory Protocol (AAP) settings are designed in but shipped in v2,
because Windows exposes no user-mode L2CAP transport and a kernel driver is
required for them.

## Decisions already made

| Topic | Decision |
|---|---|
| Target hardware | AirPods Pro 2nd gen (Lightning and USB-C case) |
| Stack | .NET 8, C#, WPF, xUnit |
| Form | Tray-resident app with popup card plus a full settings window |
| Noise control | Deferred to v2 behind a transport interface written in v1 |
| Third-party drivers | None. Hawa does not depend on MagicPods or any other driver |
| Packaging | Single-folder self-contained exe. No MSIX in v1 |
| Name | Hawa ("air" in Hindi, Urdu and Arabic) |

## Environment

- No .NET SDK is installed on the development machine. Install the .NET 8
  SDK with `winget install Microsoft.DotNet.SDK.8` before anything else.
- Git 2.55 is installed. The project directory is not yet a repository.
- AirPods must already be paired in Windows Settings. Hawa never pairs.

## Architecture

One solution, four projects.

```
Hawa.sln
  src/Hawa.Core     class library: Bluetooth, parsing, state, media, settings
  src/Hawa.Aap      class library: AAP codec + IAapTransport (NullTransport in v1)
  src/Hawa.App      WPF exe: tray, popup card, settings window, startup
  tests/Hawa.Tests  xUnit: Core and Aap unit tests
```

Dependency direction: App depends on Core and Aap. Aap depends on nothing.
Core depends on nothing except WinRT projections. Tests depend on Core and
Aap only, never on App.

### Hawa.Core

**PairedDeviceService.** Enumerates paired Bluetooth Classic devices via
`Windows.Devices.Enumeration`, selects the AirPods by name and Apple vendor
ID, and subscribes to `ConnectionStatusChanged` on the resulting
`BluetoothDevice`. Exposes `IsConnected`, `DeviceName`, `BluetoothAddress`
and a `ConnectionChanged` event. If more than one AirPods device is paired,
the user picks one in settings; the choice is persisted.

**ProximityWatcher.** Wraps `BluetoothLEAdvertisementWatcher` in active
scan mode with a manufacturer-data filter on company ID `0x004C`. Every
received advertisement is passed to `ProximityParser`. The watcher runs
whenever the app is running and a Bluetooth LE adapter exists. It does not
require the classic link to be up, because the case broadcasts while the
lid is open even before the pods connect. On `Stopped` with an error it
restarts with exponential backoff (1 s, 2 s, 4 s, capped at 30 s).

**ProximityParser.** Pure function `byte[] -> PodsSnapshot?`. Parses the
Apple Continuity proximity pairing message (type `0x07`):

| Offset | Meaning |
|---|---|
| 0 | prefix `0x01` |
| 1-2 | model ID, big endian |
| 3 | status flags: primary side, in-ear per side, both in case, "flipped" bit |
| 4 | battery: high nibble one side, low nibble the other, 0-10 steps, `0xF` unknown |
| 5 | charging flags (bits 4-6) and case battery (low nibble) |
| 6 | lid open counter and lid state |
| 7 | colour |
| 8+ | encrypted payload, ignored in v1 |

The flipped bit decides which nibble belongs to the left pod. The parser
returns `null` for anything that is not a well-formed type `0x07` message
of sufficient length. Model IDs for both AirPods Pro 2 variants are
recognised; other models are parsed but tagged so the matcher can reject
them.

**PodsSnapshot** (immutable record): `Model`, `LeftBattery`,
`RightBattery`, `CaseBattery` (each `int?` in percent, `null` when
unknown), `LeftCharging`, `RightCharging`, `CaseCharging`, `LeftInEar`,
`RightInEar`, `BothInCase`, `LidOpen`, `Rssi`, `ReceivedAt`.

**DeviceMatcher.** Decides whether a snapshot belongs to the user's
AirPods. Rule: the model must match the paired device, and among
candidates seen in the last 3 s the snapshot must have the strongest RSSI.
AirPods Pro 2 use resolvable private addresses, so address matching is
impossible without the identity key. The identity key is out of scope for
v1; a nearby stranger's AirPods Pro 2 with a stronger signal can be
mis-picked and this limitation is documented in the README.

**DeviceStateStore.** Single source of truth. Accepts accepted snapshots
and connection changes, debounces bursts (advertisements arrive several
times per second) to at most one `StateChanged` event per 250 ms, and
derives transitions: `LidOpened`, `Connected`, `Disconnected`,
`PodRemoved(side)`, `PodInserted(side)`, `LowBattery(side, percent)`.
Marks state stale when no advertisement has arrived for 30 s.

**MediaController.** Wraps
`GlobalSystemMediaTransportControlsSessionManager`. `PauseIfPlaying()`
pauses the current session only if its playback status is Playing and the
default render endpoint's `IAudioMeterInformation` peak exceeds a small
threshold within the last second. Returns whether it paused.
`ResumeIfWePaused()` resumes only when the last pause was made by Hawa
and the AirPods are still the default audio endpoint.

**AutoPauseCoordinator.** State machine driven by `DeviceStateStore`
transitions: on `PodRemoved` while the classic link is connected, call
`PauseIfPlaying()`. On `PodInserted` within 60 s of a Hawa-initiated pause,
call `ResumeIfWePaused()`. A user setting chooses whether removing one pod
or both pods triggers the pause. Disabled entirely when the setting is off.

**SettingsStore.** JSON file at `%LocalAppData%\Hawa\settings.json`,
written atomically (write temp, rename). Fields: selected device address,
auto-pause enabled, pause on one pod or both, popup on connect, popup on
lid open, popup duration seconds, start with Windows, low-battery
threshold percent, last noise mode (reserved for v2). Missing or corrupt
file falls back to defaults and is rewritten.

### Hawa.Aap

**AapCodec.** Static encoders and decoders for the byte sequences
documented by the open-source community:

- Handshake `00 00 04 00 01 00 02 00` followed by eight zero bytes.
- Enable features `04 00 04 00 4D 00 FF 00` followed by six zero bytes.
- Request notifications `04 00 04 00 0F 00 FF FF FF FF`.
- Set noise mode: opcode `0x09`, modes Off=1, ANC=2, Transparency=3,
  Adaptive=4.
- Decode ear-detection notification (prefix `04 00 04 00 06 00`).
- Decode battery notification (prefix `04 00 04 00 04 00`).

**IAapTransport.** `Task ConnectAsync()`, `Task SendAsync(byte[])`,
`event Action<byte[]> Received`, `bool IsAvailable`, `Task DisconnectAsync()`.

**NullTransport.** `IsAvailable` is false; every call throws
`AapUnavailableException`. v1 ships only this. v2 adds a driver-backed
transport without touching the codec or the UI.

**NoiseControlService.** Uses the codec and a transport. In v1 it exposes
`IsAvailable == false` so the UI can render the noise-control page in a
disabled state with an explanation.

### Hawa.App

**TrayIcon.** Uses the H.NotifyIcon.Wpf package. Icon variants: normal,
low battery, disconnected, no adapter. Tooltip shows the lowest of
left/right battery. Hover shows the popup card. Left click toggles the
card. Right click opens a context menu: Show settings, Noise control
submenu (disabled in v1 with a tooltip), Auto-pause toggle, Start with
Windows toggle, Quit.

**PopupCard.** Borderless, rounded (16 px), translucent window positioned
above the tray. Contents: AirPods Pro image, device name, three battery
rings (left, right, case) with percentages and a charging bolt when
charging, greyed ring when unknown. Slides up and fades in over 200 ms,
fades out after the configured duration (default 5 s). Shown on `LidOpened`
and `Connected` transitions when the respective settings are on. Never
steals focus (`ShowActivated = false`, `WS_EX_NOACTIVATE`).

**SettingsWindow.** Uses the WPF-UI (lepoco) package for Fluent styling
and a navigation sidebar. Pages:

- Device: paired device picker, name, model, connection state, last seen.
  If nothing is paired, shows instructions and a button that runs
  `ms-settings:bluetooth`.
- Noise control: four mode cards, all disabled, with the note "Needs the
  Hawa AAP driver, coming in v2".
- Behaviour: auto-pause on/off, pause on one or both pods, popup on
  connect, popup on lid open, popup duration, start with Windows,
  low-battery threshold.
- About: version, licence, link to the repository.

**StartupRegistration.** Writes or removes a `Hawa` value in
`HKCU\Software\Microsoft\Windows\CurrentVersion\Run` pointing at the exe.

**Single instance.** A named mutex prevents a second copy; launching again
brings the settings window to the front.

## Data flow

```
BLE advert --> ProximityWatcher --> ProximityParser --> DeviceMatcher
                                                            |
Classic link ------> PairedDeviceService ----------> DeviceStateStore
                                                            |
                                     +----------------------+---------------+
                                     v                      v               v
                               PopupCard/TrayIcon   AutoPauseCoordinator   Settings UI
                                                            |
                                                      MediaController
```

All Bluetooth callbacks arrive on thread-pool threads. `DeviceStateStore`
raises `StateChanged` on the thread that produced it; the App layer
marshals to the WPF dispatcher.

## Error handling

| Condition | Behaviour |
|---|---|
| No Bluetooth adapter or LE unsupported | Tray icon shows the "no adapter" variant; tooltip and Device page explain. Watcher not started. |
| No AirPods paired | Device page shows pairing instructions; tray tooltip says "No AirPods paired". |
| Advertisement watcher stops with error | Restart with backoff; after 5 failures show a tray balloon once. |
| Media session manager unavailable | Auto-pause silently disabled; Behaviour page shows a warning. |
| Settings file corrupt | Log, back up as `settings.json.bad`, recreate defaults. |
| Unhandled exception | Logged to `%LocalAppData%\Hawa\logs\`, app stays alive where possible. |

Logging uses `Microsoft.Extensions.Logging` with a rolling file provider.

## Testing

`tests/Hawa.Tests` (xUnit):

- `ProximityParserTests`: byte fixtures for lid open, lid closed, one pod
  in ear, both in case, charging, unknown battery, flipped bit set,
  non-Apple and malformed payloads. Fixtures are recorded from the real
  device during development and committed as hex strings.
- `DeviceMatcherTests`: model mismatch rejected, strongest RSSI wins,
  stale candidates expire.
- `DeviceStateStoreTests`: debounce, each transition fires exactly once,
  stale marking.
- `AutoPauseCoordinatorTests`: pause only when connected and playing,
  resume only after Hawa paused and within 60 s, setting for one or both
  pods, disabled setting.
- `AapCodecTests`: every encoder produces the documented bytes; decoders
  round-trip.
- `SettingsStoreTests`: defaults, round-trip, corrupt file recovery.

`MediaController` and the WinRT wrappers are hidden behind interfaces so
the coordinator and store are tested with fakes.

Manual checklist (kept in `docs/manual-test.md`): popup on lid open, popup
on connect, tray tooltip values, auto-pause and resume with Spotify and a
browser, start with Windows, no-adapter behaviour with the adapter
disabled, second-instance behaviour.

## Out of scope for v1

Noise control, conversation awareness, press-and-hold remapping, rename,
1 % battery precision, identity-key extraction for exact device matching,
AirPods models other than Pro 2 (they may partially work), MSIX packaging,
auto-update, localisation.

## v2 direction (not designed here)

A KMDF filter driver that bridges L2CAP PSM `0x1001` to a device interface,
test-signed for personal use, plus a `DriverTransport : IAapTransport`. The
codec, `NoiseControlService`, and the noise-control UI already exist, so v2
is transport plus driver only.
