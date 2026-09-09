# Security

## Release verification

Official releases contain the setup EXE and a `SHA256SUMS.txt` generated from that exact file. Download both from this repository's Releases page and compare them with `Get-FileHash` as shown in the README.

The installer is currently unsigned. A matching checksum proves that a download matches the file published by this repository; it does not provide the identity guarantees of an Authenticode certificate.

## Privileged operations

Setup requests administrator access because the default destination is Program Files (x86), shortcuts are created for all users, uninstall information is written under HKLM, and the retail Indeo codec is installed under SysWOW64. The normal game launcher runs as the current user.

Setup also mounts user-selected ISO media, extracts legacy InstallShield/ZIP content, patches the selected retail executable with ZipperFixup, and writes per-user game registry settings. Pirate's Moon RIP inputs are accepted only when both executable hashes and the expected directory layout match the supported release; NFO, log, registry, uninstaller-state, and `CRACK` directory debris are not copied into the installed game. These behaviors are required by the preservation workflow and are disclosed because security products may treat them as unusual.

Setup never asks users to disable antivirus software, SmartScreen, User Account Control, or other Windows security features.

## Reporting a vulnerability

Use GitHub's private vulnerability reporting feature for this repository. If that feature is unavailable, open a minimal issue asking the maintainer for a private contact channel; do not post exploit details, personal paths, or secrets publicly.
