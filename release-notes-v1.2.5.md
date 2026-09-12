# MechWarrior 3 Remastered v1.2.5

This update fixes a clean-install registry gap discovered through community reports. Players no longer need to run the original 1999 setup program before starting the remaster.

## Changes

- Setup writes `InstallPath`, `Version`, and the complete `InstallOptions=0x00050707` value to the canonical 32-bit Windows machine registry key for MechWarrior 3 and, when selected, Pirate's Moon.
- Registration happens automatically during the normal remaster installation. No manual registry import, legacy setup run, or restart is required.
- A separate retail installation already registered at another path is preserved.
- The launcher continues to refresh the tested per-user VirtualStore registration as a compatibility fallback.
- Uninstall removes only registrations that still point to that remaster installation.
- GitHub now provides a guided bug-report form requesting reproducible system and diagnostic information without asking users to upload game media or private data.

## Verification

- Setup, launcher, uninstaller, and registry-focused test programs compiled successfully.
- The registry contract passed for the base game and Pirate's Moon, including canonical 32-bit paths, the VirtualStore fallback, versions, path normalization, complete install options, and preservation of a differently located retail installation.
- Full installer smoke verification passed for the base game and Pirate's Moon RIP path: 62 payload files, no forbidden files, qualified r18 DDrawCompat payload unchanged, ZipperFixup passed, and CD-audio protocol/lifetime checks passed.
- The setup, extracted payload, base-game smoke tree, and Pirate's Moon smoke tree produced no new Microsoft Defender detection with security intelligence `1.459.177.0` (12 detections before and after). The newest pre-scan event was a blocked reflection-heavy development command for a disposable registry integration test, not a packaged file.

Setup SHA-256: `5FE8FDE29257CB2276AF60B2CD610CAD6CDAC7DD4FBBDD6AD136D4E339413255`.

Packaged DDrawCompat SHA-256: `FD11B9B6B8A8CC23744DFEDC10E23798860F3A96BD8B2B4C75DD2FDB3BE8F8FB`.

## Known limitation

The canonical HKLM write is covered by the compiled registry contract, but this build still needs confirmation from a genuinely clean Windows account or machine that has never run the retail installer.

The installer remains unsigned, so Windows may display an Unknown publisher or SmartScreen reputation warning. Verify the published checksum; do not disable Windows security protections.
