# Hawa manual test

> Task 11 fills in the full manual checklist. Keep the "Captured adverts" section below
> when you do — it is real hardware data, not a placeholder.

## Captured adverts

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
| 8 | `11` | Lid byte. `data[8] & 0x08 == 0`, so the parser reports `LidOpen = true` |
| 9 | `00` | Colour |

### Open item: lid-bit polarity is unverified

190 adverts were captured across the session. Only the encrypted tail (bytes 11
onward) varied; every one carried the identical plaintext header
`07190124200B998F1100`, so byte 8 never moved off `0x11` and only one lid state
was observed. The true state of the case at capture time is unknown. The lid
comparison in `ProximityParser` (`(data[8] & 0x08) == 0` means open) was
therefore left as written.

To close this out, run Hawa with the AirPods case nearby, open the lid, note an
advert line, close the lid, note another, and compare byte 8 between the two. If
the parser reports `LidOpen = false` for the lid-open capture, invert the
comparison to `(data[8] & 0x08) != 0`, flip the `lid` bytes in the two constants
in `ProximityParserTests`, and re-run the tests.

Note also that `Paired AirPods: 0` was logged during this capture: the AirPods
advertise over BLE but are not paired to this PC over Bluetooth Classic, so the
tray sat in its "no AirPods paired" state. Pair them before re-testing.
