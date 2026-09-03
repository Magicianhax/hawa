# Hawa manual test checklist

Run `dotnet run --project src/Hawa.App` with AirPods Pro 2 paired.

| # | Step | Expected | Pass |
|---|---|---|---|
| 1 | Start Hawa | Earbud icon in tray, tooltip "not in range" until the case opens | pending |
| 2 | Open case lid | Card appears bottom-right within 2 s with L/R/case %, fades after 5 s | pending |
| 3 | Put pods in ears | Windows connects; card appears again (popup on connect) | pending |
| 4 | Play Spotify, remove left pod | Playback pauses within 1 s | pending |
| 5 | Reinsert within 60 s | Playback resumes | pending |
| 6 | Remove pod, wait 70 s, reinsert | Playback stays paused | pending |
| 7 | Pause Spotify manually, remove pod, reinsert | Nothing resumes | pending |
| 8 | Play YouTube in a browser, remove pod | Pauses | pending |
| 9 | Settings > Behaviour > disable auto-pause, remove pod | Nothing pauses | pending |
| 10 | Hover tray icon | Card shows for 3 s | pending |
| 11 | Left-click tray icon twice | Card toggles on then off | pending |
| 12 | Type in Notepad while card appears | Notepad keeps focus | pending |
| 13 | Enable Start with Windows, sign out and in | Hawa is running | pending |
| 14 | Disable Bluetooth in Windows, start Hawa | Grey icon with red slash, Device page shows the adapter warning | pending |
| 15 | Start a second Hawa.exe | Settings window of the first instance comes to front | pending |
| 16 | Close lid, walk out of range 30 s | Tooltip shows "not in range" | pending |
| 17 | With `HAWA_TRACE=1` set, put the pods in the case and compare the `advert` log byte at offset 8 with the lid open vs. closed | `LidOpen` in the tooltip/card matches the byte 8 comparison for each lid state | pass (2026-09-04: open `0x51`, closed `0x5A`) |
| 18 | With one pod in an ear | The log's status byte reflects the in-ear pod, and auto-pause fires when it is removed | pass for the primary pod; the other pod's removal is reported late by the AirPods (see below) |

The AirPods Pro 2 used for development are not paired to this PC over Bluetooth Classic
(they were only observed advertising over BLE), so every row above is marked pending
rather than pass/fail. Rows 1 and 15 were exercised against the self-contained
published build (see README's Publish section) and are recorded in the review section
of `tasks/todo.md`.

## Captured adverts

Per-advert `advert` lines are logged at Verbose level and are **off by default**: a
busy room produces several per second per Apple device, which is gigabytes over a
working day. To capture them, set `HAWA_TRACE=1` in the environment Hawa is started
from and restart it:

```
$env:HAWA_TRACE = "1"; .\publish\Hawa.exe
```

Without that variable the log stops at Debug and contains no `advert` lines, so any
row below that reads the log needs it. The log file rolls at 10 MB.

Captured on 2026-09-03 from `%LocalAppData%\Hawa\logs\hawa-20260903.log` while
`Hawa.exe` was running on the development machine. The manufacturer-data payload
follows Apple company ID `0x004C`.

```
2026-09-03 23:57:42.821 +05:00 [DBG] advert 5D4EBBA761D8 rssi -64 data 07190124200B998F1100055A92DB179078D3AE88907B40DB9D25E2
2026-09-03 23:57:44.131 +05:00 [DBG] advert 5D4EBBA761D8 rssi -58 data 07190124200B998F1100055A92DB179078D3AE88907B40DB9D25E2
```

Decoded by `ProximityParser` against the payload `07190124200B998F11...`:

| Offset | Byte | Meaning |
| --- | --- | --- |
| 0 | `07` | Proximity Pairing message type |
| 1 | `19` | Length (25) |
| 2 | `01` | Prefix |
| 3-4 | `24 20` | Model `0x2420` = AirPods Pro (2nd generation, USB-C) |
| 5 | `0B` | Status: `0x20` clear so the left/right nibbles are flipped; ear bits `0b1011` = both pods in ear |
| 6 | `99` | Both pods 90% |
| 7 | `8F` | Charge bits `0x8`; case battery nibble `0x0F` = unknown |
| 8 | `11` | Lid byte. Bit `0x08` is clear, but the case nibble at offset 7 is `0xF`, so this advert came from a pod outside the case and the parser reports `LidOpen = false`. The bit is therefore not evidence of the polarity either way |
| 9 | `00` | Colour |

### Verified on hardware (2026-09-04, AirPods Pro 2 USB-C, firmware as shipped)

Captured with `HAWA_TRACE=1`, one action at a time. Header bytes are offsets 5-8
(status, batteries, charge|case, lid).

| State | Header | Reading |
| --- | --- | --- |
| Both in case, lid open, unplugged | `55 88 B8 51` / `35 88 B8 51` | each pod advertises; charge bits 0x1, 0x2 (pods), case 80 %, lid bit clear = open |
| Same, case plugged in | `55 89 F8 51` | charge nibble gains 0x4 = case charging |
| Both in case, lid closed | `55 99 B8 5A` | lid bit 0x08 set = closed |
| First pod inserted | `03 99 8F 13` | bit 0x2 = advertising (primary) pod in ear; case nibble F = pod-originated |
| Both worn | `0B 99 8F 13` | bit 0x8 = other pod in ear |
| Primary pod removed | `09`, or handover to the other pod's address with `23` | reported within ~2 s |
| Other pod removed | `03` / `23` | reported 18 s to several minutes later, often only when the pod is touched again |

Conclusions: lid polarity, charging bits, battery nibbles and in-ear bits match the
parser as written. The `0x20` status bit distinguishes which pod is advertising
(the "flipped" rule swaps the left/right nibbles accordingly). Ear state for the
non-advertising pod is unreliable over BLE, which is an AirPods firmware behaviour;
AAP over the v2 driver is the fix. Holding a removed pod in your hand can make it
report "in ear" again after a few seconds, so put it down during tests.

The popup never took focus: `focus` probe lines in the log showed the foreground
window unchanged before, right after, and 700 ms after each show.
