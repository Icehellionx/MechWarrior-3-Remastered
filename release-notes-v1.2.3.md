# MechWarrior 3 Remastered v1.2.3

This update removes a startup race between Windows ISO mounting and the game launch.

## Changes

- The launcher now waits until Windows has assigned the mounted disc a drive letter and its root is readable before starting MechWarrior 3 or an ISO-installed Pirate's Moon.
- Media readiness is checked for both newly mounted and already attached images, with a bounded 30-second timeout and a specific recovery message.
- Successful mount readiness, including the selected drive and volume label, is recorded in `%LOCALAPPDATA%\MechWarrior 3 Remastered\launcher.log` to distinguish media failures from graphics initialization failures.
- The launcher invokes its mount script through PowerShell's encoded-command interface so spaces, apostrophes, and other path characters cannot corrupt the command line.

The existing video-initialization recovery remains unchanged. A confirmed `Video Error` may still indicate a separate graphics compatibility issue; attach `mech3.out`, `DDrawCompat-Mech3fixup.log`, and `launcher.log` when reporting one.

## Verification

The launcher was compiled and tested against a real MW3 ISO. Windows attached the image, exposed the `MW3` volume, and the launcher confirmed its root was readable before returning. The already-mounted path was also verified.

The finished setup executable passed a Microsoft Defender custom scan with security-intelligence version `1.459.146.0` and no detection.

Download `SHA256SUMS.txt` with the setup executable and compare it using the command in the README. `PAYLOAD_MANIFEST.sha256` lists every file embedded inside setup.

The installer remains unsigned, so Windows may display an Unknown publisher or SmartScreen warning.
