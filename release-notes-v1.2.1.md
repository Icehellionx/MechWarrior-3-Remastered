# MechWarrior 3 Remastered v1.2.1

This polish update handles the blocking video-device error seen on the first launch of some fresh installations and raises the initial music volume.

## Changes

- The launcher now watches the game process for its `Video Error` dialog during startup.
- If `Failed to initialize video device` blocks startup, the launcher dismisses that game-owned dialog, performs its normal cleanup, and retries automatically.
- Existing log-based detection remains in place as a second path for the related early video-startup failure.
- CD music now starts at 60% instead of 10%.
- Remaster defaults are applied once per game, so later volume and graphics changes made by the player are no longer reset on every launch.

## Verification

Download `SHA256SUMS.txt` with the setup executable and compare it using the command in the README. `PAYLOAD_MANIFEST.sha256` lists every file embedded inside setup.

The installer is not Authenticode-signed. Windows may therefore display an Unknown publisher or SmartScreen warning.
