# MechWarrior 3 Remastered v1.2.2

This reliability update makes video-startup recovery deterministic and replaces the CD-audio executable that Microsoft Defender identified heuristically.

## Changes

- The launcher restores the tested Direct3D HAL, primary adapter, and 1024x768 startup mode before launch and again after each detected video-device failure.
- CPU-affinity rotation is disabled in the shipped graphics preset so legacy D3DIM initialization remains on one processor.
- Automatic retries now allow four seconds for graphics-driver teardown and record attempt results in `%LOCALAPPDATA%\MechWarrior 3 Remastered\launcher.log`.
- The upstream native `cdaudioplr.exe` helper is replaced with a small, auditable managed implementation of the same local cdaudio-winmm protocol.
- The replacement helper does not enumerate processes or open process handles. Microsoft Defender scans of the development build completed with no detection.
- The installer does not add antivirus exclusions. Re-recording the MP3 tracks is unnecessary because the reported detection targeted executable code, not music media.

## Verification

Download `SHA256SUMS.txt` with the setup executable and compare it using the command in the README. `PAYLOAD_MANIFEST.sha256` lists every file embedded inside setup.

The installer remains unsigned, so Windows may display an Unknown publisher or SmartScreen warning.
