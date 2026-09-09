# MechWarrior 3 Remastered v1.1.0

This update makes Pirate's Moon and the original documentation easier for normal players to use.

## Changes

- Pirate's Moon now accepts its common RIP `.zip` directly.
- An already-extracted RIP folder is also supported.
- Original Pirate's Moon ISO installation remains available.
- RIP executables are recognized by SHA-256; the NFO, logs, registry file, old uninstall state, and `CRACK` directory are not copied into the installed game.
- RIP-based Pirate's Moon installations no longer ask for source media when launching.
- The original MechWarrior 3 and Pirate's Moon manuals are installed under `Manuals`.
- Both manuals have direct shortcuts in the Start-menu project group.

## Verification

Download `SHA256SUMS.txt` with the setup executable and compare it using the command in the README. `PAYLOAD_MANIFEST.sha256` lists every file embedded inside setup.

The installer is not Authenticode-signed. Windows may therefore display an Unknown publisher or SmartScreen warning.

## Test coverage

The US MW3 path passed its complete extraction/patching smoke test. Both the extracted-folder and ZIP forms of the common Pirate's Moon RIP were tested through no-disc executable selection and ZipperFixup. A complete Pirate's Moon ISO remains unavailable for an end-to-end expansion ISO test.
