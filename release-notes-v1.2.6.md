# MechWarrior 3 Remastered v1.2.6

This compatibility update addresses community-reported setup, media, audio, input, display, and removal problems.

## Changes

- Pirate's Moon installations accept the common ISO-version ZIP containing a mixed-mode BIN/CUE image. Setup validates and extracts the ZIP, converts only the ISO 9660 Mode-1 data volume to a temporary mountable image, installs the disc, and removes staging afterward. The verified true-disc executable receives a deterministic 12-byte runtime-path normalization plus two disc-check branch changes; the partly normalized RIP retail executable needs only the two branch changes. Both become the same verified no-disc executable, and the user's media is never modified.
- ISO-derived Pirate's Moon installs now receive the 108-byte `DATA.TAG` application marker normally created by the original InstallShield setup. Its absence caused repeatable `0xC0000005` startup failures; the corrected exact BIN/CUE archive output holds a real Pirate's Moon game window stable under the launcher.
- Setup creates writable `keys` directories, and the launcher checks them before every launch. This removes the missing-storage failure that prevented fresh installs from saving keyboard, mouse, joystick, and HOTAS mappings while preserving existing profiles. Older unwritable Program Files installs are diagnosed without blocking gameplay.
- Both games keep the required 640×480 startup-video mode and the tested 1024×768 gameplay mode, which DDrawCompat scales to the desktop resolution. Changing resolution from the legacy in-game menu remains unsupported because suppressing 640×480 also prevents startup videos from opening.
- Input-support payload verification now checks `IFORCE2.dll`, `force_eff.ifr`, and writable control-profile storage for both games.
- The branded desktop and Start-menu shortcut opens the unified selection launcher with no direct-game argument. Setup removes only recognized legacy project PowerShell shortcuts that bypassed the hub and displayed the old icon.
- Launcher-owned ISO cleanup now retries and polls Windows until the image reports `Attached=False`; it no longer reports an eject merely because the first dismount command returned.

## Verification status

- Focused control-profile and Pirate's Moon executable-transformation tests pass.
- Full installer build and base/Pirate's Moon RIP smoke verification pass with 65 payload files and zero forbidden files.
- Base ISO, Pirate's Moon ISO-version ZIP, and Pirate's Moon RIP verification share one parameterized installed-game contract covering mount lifetime where applicable, extraction, executable hashes, compatibility output, every installed music track, CD-audio lifecycle/protocol, input files, writable mappings, renderer policy, and wrapper provenance. The base ISO additionally verifies populated disc video. All three available media paths pass; the exact ISO-version archive passes safe ZIP extraction, BIN/CUE conversion, mount/eject, InstallShield extraction, and true-disc executable normalization.
- CD music no longer depends on the failing legacy `mpegvideo` MCI driver. The helper now uses pinned MIT-licensed NLayer 1.16.0 for managed MP3 decoding and Windows `waveOut` for output. On the current validation account, the real-output gate sees eight devices and passes decode, play, pause, resume, and stop both directly and through each installed game's helper protocol.
- The launcher now has a bottom-right **UNINSTALL** action. It removes both game trees, compatibility files, settings, diagnostics, and shortcuts while preserving only base-game and Pirate's Moon `pilots` save trees. Save copies are SHA-256 verified, unsafe linked paths are rejected, the cleanup worker independently validates the installation markers and rejects filesystem roots, and failed cleanup retains a recoverable backup. Cleanup clears read-only attributes from ISO-derived files before removal and runs outside the install directory so Windows can remove that directory. Reinstall accepts the resulting save-only folder and stages those saves into the new installation before replacing it.
- Defender custom scans of the final setup and verification trees produced no new or active detection with intelligence `1.459.193.0`.
- Setup SHA-256: `221D2DE3972A9116BBF74C07EF7360BB5731442BA6F938C6A0149031DDDBA897`.

## Known limitation

Physical HOTAS enumeration and remapping still require field confirmation on representative hardware. The replacement MP3 path passes on the current interactive validation account but still needs representative field-machine coverage.

One user reported a strong green cast across base-game mission one on a first affected launch; rebooting Windows cleared it and no recurrence has been reported. The capture retained non-green HUD colors, and no matching renderer logs or hardware/HDR details are available. This is tracked as an unresolved, non-reproduced field observation and is not evidence for changing the qualified renderer baseline by itself.
