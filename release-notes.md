# MechWarrior 3 Remastered v1.0.0

Initial public installer release.

## Included

- MW3 extraction from a user-selected original ISO.
- Official v1.2 update.
- Modern-Windows compatibility through ZipperFixup and the project's DDrawCompat build.
- Faithful presentation shader and CD-music support.
- Optional Pirate's Moon installation from user-selected media.
- Windows shortcuts and uninstaller.

## Verification

Download `SHA256SUMS.txt` with the setup executable and compare it using the command in the README. `PAYLOAD_MANIFEST.sha256` lists every file embedded inside setup.

The installer is not Authenticode-signed. Windows may therefore display an Unknown publisher or SmartScreen warning.

## Known limitation

The US MW3 installation path passed the complete automated smoke test. Pirate's Moon executable patching was tested, but a complete expansion ISO was unavailable for an end-to-end media test.
