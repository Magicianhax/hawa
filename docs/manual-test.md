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
| 17 | With `HAWA_TRACE=1` set, put the pods in the case and compare the `advert` log byte at offset 8 with the lid open vs. closed | `LidOpen` in the tooltip/card matches the byte 8 comparison for each lid state | pending |
| 18 | With one pod in an ear | The log's status byte reflects the in-ear pod, and auto-pause fires when it is removed | pending |

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

### Open item: lid-bit polarity is unverified

190 adverts were captured across the session. Only the encrypted tail (bytes 11
onward) varied; every one carried the identical plaintext header
`07190124200B998F1100`, so byte 8 never moved off `0x11` and only one lid state
was observed. Every one also carried case nibble `0xF`, the signature of a pod
advertising outside the case, so none of them can say anything about a lid. The
capture can neither confirm nor refute the polarity of bit `0x08`.

The parser now reads the lid only from case-originated adverts: `LidOpen` is true
when the case battery nibble is a real value (0-10) **and** bit `0x08` is clear.
The polarity of that bit is still unverified, and the header above is pinned as a
test fixture asserting `LidOpen = false` for it.

To close this out, run Hawa with `HAWA_TRACE=1` and the AirPods case nearby, open
the lid, note an advert line whose offset-7 low nibble is 0-10, close the lid, note
another, and compare byte 8 between the two. If the parser reports
`LidOpen = false` for the lid-open capture, invert the comparison to
`(data[8] & 0x08) != 0`, flip the `lid` bytes in the two constants in
`ProximityParserTests`, and re-run the tests.

Note also that `Paired AirPods: 0` was logged during this capture: the AirPods
advertise over BLE but are not paired to this PC over Bluetooth Classic, so the
tray sat in its "no AirPods paired" state. Pair them before re-testing.
