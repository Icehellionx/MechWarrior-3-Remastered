# MechWarrior 3 Remastered v1.2.7

This update adds a reversible, experimental sound-effect level option and a mounted-disc-folder route for Wine users. The installer still requires user-supplied game media.

## Changes

- Setup can use a readable already mounted MechWarrior 3 disc folder. The launcher reuses that folder without trying to eject its mount. On Linux, the mount must remain available as a CD-ROM mapping in the Wine prefix. A complete Wine install and game launch have not been verified.
- Setup offers an unchecked experimental option to lower seven unusually loud base-game sound effects. It derives the changed archives from exact-hash US v1.2 game media during installation, retains byte-identical originals under `MechWarrior 3\OriginalSoundArchives`, and includes `Use-SoundLevelCandidate.ps1` to restore them. No original game audio is included in the downloadable setup. Pirate's Moon sound and CD music are unchanged.
- The launcher adds a **CONTROLS** action with the game's remapping steps and a way to open Windows Game Controllers for joystick calibration. Setup and launch continue to check writable per-game `keys` storage; the installed-tree test checks the original DirectInput/force-feedback files.

## Verification

- The installer build and real MechWarrior 3 ISO plus Pirate's Moon RIP smoke tests passed: 66 payload files, zero forbidden files. The base ISO test covered mount/eject, borrowed-folder validation, extraction, startup videos, official patching, and installed-game checks.
- Both leveled sound banks matched their reviewed hashes. The original banks were restored byte-for-byte in the smoke-installed game. Payload manifest and setup checksum matched the final build.
- Microsoft Defender custom scans of the setup, extracted payload, and both smoke-installed game trees found no new detection with signature `1.459.384.0` (20 before and after).
- Setup SHA-256: `3DCC39E89AADBEFCD58243DFBE97F689736EA4D6CE6DE0B8CB33268283B4BEAC`.

## Still to verify

The reported launch sound has not been confirmed by listening in its mission. Physical joystick axis binding and force feedback, a complete Wine install/launch, and the unresolved RTX 5080 field report need tests on affected hardware. The sound option reduces gain; it cannot repair clipping already present in the retail samples.

Previous release: [v1.2.6](https://github.com/Icehellionx/MechWarrior-3-Remastered/releases/tag/v1.2.6).
