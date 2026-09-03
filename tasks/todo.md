# Hawa v1

Plan: docs/superpowers/plans/2026-09-03-hawa-v1.md

- [x] Task 0 scaffold
- [x] Task 1 AAP codec
- [x] Task 2 proximity parser
- [x] Task 3 device matcher
- [x] Task 4 device state store
- [x] Task 5 settings store
- [x] Task 6 media controller + auto-pause
- [x] Task 7 WinRT Bluetooth adapters
- [x] Task 8 app shell + tray
- [x] Task 9 popup card
- [x] Task 10 settings window + startup
- [x] Task 11 behaviour wiring, docs, publish

## Review

**Hardware facts.** The development machine's AirPods Pro 2 (USB-C) were only ever
observed advertising over Bluetooth LE; they are not paired to this PC over Bluetooth
Classic (`Paired AirPods: 0` in every capture). All in-app UI paths that depend on a
paired device (battery card, connect popup, auto-pause, tray tooltip battery text)
could not be exercised against real hardware in this environment.

**Advert fixtures.** 190 adverts were captured in Task 8 from the running dev build's
log; see `docs/manual-test.md` for the full decode. Every capture carried the same
plaintext header, so byte 8 (the lid bit) never changed value across the session —
only one lid state was observed. The lid bit therefore was **not** inverted; the
existing comparison (`(data[8] & 0x08) == 0` means open) was left as written, and is
flagged as an open item pending a real open/closed comparison (checklist row 17).
The in-ear status bits are similarly unverified against a real in-ear/out-of-ear
transition (checklist row 18).

**WPF-UI substitutions.** None. No control in `Settings/` or `Popup/` was swapped for
a WPF-UI equivalent; everything is plain WPF plus `H.NotifyIcon` for the tray icon,
as specified in the plan.

**Publish.** `dotnet publish src/Hawa.App -c Release -r win-x64 --self-contained -o publish`
succeeded with 0 warnings/errors. The published folder is self-contained (includes the
.NET 8 runtime) and is **193 MB** across 284 files. `publish/` stays gitignored.

**Checklist results** (full table in `docs/manual-test.md`):
- Row 1 (start Hawa, tray/log) — **verified** against the published exe: log recorded
  `Bluetooth LE supported: true`, `BLE watcher started`, and `Hawa started`.
- Row 15 (second instance brings the first's settings window to front) — **verified**:
  launching a second `Hawa.exe` left only the original process running, now with a
  visible main window titled "Hawa"; the second process exited.
- Rows 2-14, 16-18 — **pending**. They require paired AirPods Pro 2 and are not
  operable from this environment; see `docs/manual-test.md` for exact steps.
- The published Hawa instance was stopped after the smoke test; nothing was left
  running.

## Final review fix wave

One commit resolving the 8 Important findings and two promoted minors from
`.superpowers/sdd/2026-09-03-hawa-v1/final-review.md`. Full write-up in
`.superpowers/sdd/2026-09-03-hawa-v1/final-fix-report.md`.

- **F1 hover vs click** — `PopupCard` tracks `_pinned`; hover uses the new `ShowOnHover()`
  and is a no-op while the card is up, so a click no longer hides what hover just showed.
  `ShowSettings` now subscribes `Closed` only in the branch that creates the window.
- **F2 matcher hysteresis and address change** — `DeviceMatcher` keeps the current address
  until it times out or a rival beats it by 8 dB; `DeviceStateStore.Derive` skips ear
  transitions across an address change. Three tests cover the band, the take-over and the skip.
- **F3 logging** — per-advert lines moved to Verbose behind `HAWA_TRACE=1`; the log file is
  capped at 10 MB and rolls. Verified in both modes against the built exe.
- **F4 media availability** — `IMediaSession.IsAvailableAsync()` is probed once at startup,
  auto-pause is forced off when it fails, and the Behaviour page shows a warning InfoBar.
- **F5 rolling peak** — `NAudioProbe` samples the cached endpoint every 100 ms into a
  ten-slot ring and reports the maximum, so the audible check covers the last second.
- **F6 radio state** — LE support now also requires the radio to be on, and `AppServices`
  follows `Radio.StateChanged`, starting or stopping the watcher and raising
  `BluetoothAvailabilityChanged` for the tray and the settings view model.
- **F7 README** — the known limitation no longer claims Pro 2 targeting the matcher does not
  enforce, and describes the sticky-address behaviour.
- **F8 lid from case adverts** — the lid bit is read only when the case battery nibble is a
  real value, so pods advertising outside the case no longer report "lid open". The real
  captured header is pinned as a fixture and `docs/manual-test.md` is corrected.

Build: 0 warnings, 0 errors. Tests: 55 passed, 0 failed (52 before, 3 added).
