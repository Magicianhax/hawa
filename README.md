# Hawa

AirPods on Windows, the way iOS does it: a battery card when you open the case, in-ear detection
that pauses and resumes your music, and a tray icon that always knows your battery.

Hawa means "air" in Hindi, Urdu and Arabic.

## What works (v1)

- Battery (left, right, case) and charging status
- Card on lid open and on connect
- Auto-pause when you take a pod out, auto-resume when you put it back
- Low-battery notification
- Settings window, start with Windows

## What does not work yet

Noise control (ANC / Transparency / Adaptive) and every other setting need Apple's AAP protocol
over an L2CAP channel. Windows offers no user-mode way to open one, so v2 will ship a small
kernel driver. The protocol layer is already in `src/Hawa.Aap`.

## Known limitation

AirPods rotate their Bluetooth LE address, so Hawa picks the AirPods Pro 2 with the strongest
signal. Someone else's AirPods Pro 2 very close to you can occasionally be shown instead.

The in-ear and lid-open bit mappings are taken from community documentation and are being
confirmed against real captures (see docs/manual-test.md).

## Build

    winget install Microsoft.DotNet.SDK.8
    dotnet test
    dotnet run --project src/Hawa.App

## Publish

    dotnet publish src/Hawa.App -c Release -r win-x64 --self-contained -o publish

Run `publish\Hawa.exe`.

## Requirements

Windows 11, a Bluetooth adapter with Low Energy, AirPods already paired in Windows Settings.
