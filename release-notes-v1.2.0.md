# MechWarrior 3 Remastered v1.2.0

This update adds a unified remaster launcher and smooths over the game's intermittent video-startup failure.

## Changes

- The desktop shortcut now opens a black-and-red remaster hub.
- The hub provides one-click access to MechWarrior 3, Pirate's Moon, and both original PDF manuals.
- Game tiles use the icon embedded in each installed game executable.
- The launcher keeps the original red Mad Cat icon artwork and adds only a small steel `R` identifier.
- If the game writes its known `Error opening video... ABORTING RUN` startup failure and exits immediately, the launcher cleans up and retries automatically up to four times.
- Existing `mw3` and `pm` launcher command-line arguments remain supported for compatibility.

## Verification

Download `SHA256SUMS.txt` with the setup executable and compare it using the command in the README. `PAYLOAD_MANIFEST.sha256` lists every file embedded inside setup.

The installer is not Authenticode-signed. Windows may therefore display an Unknown publisher or SmartScreen warning.
