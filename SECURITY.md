# Security

## Release verification

Official releases contain the setup EXE and a `SHA256SUMS.txt` generated from that exact file. Download both from this repository's Releases page and compare them with `Get-FileHash` as shown in the README.

The installer is currently unsigned. A matching checksum proves that a download matches the file published by this repository; it does not provide the identity guarantees of an Authenticode certificate.

## Privileged operations

Setup requests administrator access because shortcuts are created for all users, required 32-bit game and uninstall information is written under HKLM, and the retail Indeo codec is installed under SysWOW64. The default game destination is the current user's local Programs directory, and the normal game launcher runs as that user without elevation.

Setup also mounts user-selected ISO media, extracts legacy InstallShield/ZIP content, patches the selected retail executable with ZipperFixup, and writes the games' required 32-bit machine-wide installation metadata. Pirate's Moon ISO-version ZIPs are path-validated before extraction; a single-file `TRACK 01 MODE1/2352` BIN/CUE layout is required, raw-sector sync/mode bytes and ISO 9660 metadata are checked, and only the data track is converted to a temporary mountable ISO. The launcher writes per-user game settings and a compatibility registration fallback. Pirate's Moon RIP inputs are accepted only when both executable hashes and the expected directory layout match the supported release; NFO, log, registry, uninstaller-state, and `CRACK` directory debris are not copied into the installed game. These behaviors are required by the preservation workflow and are disclosed because security products may treat them as unusual.

Uninstall copies only the two known `pilots` save trees to a unique temporary backup, verifies each copied file with SHA-256, removes the installation, and restores those saves to their original paths. The backup is deleted only after verified restoration; if cleanup fails, its recovery path is shown to the user. Reparse-point save paths are rejected rather than followed. Reinstall accepts an existing destination only when it is empty or contains this exact save-only directory shape.

Setup never asks users to disable antivirus software, SmartScreen, User Account Control, or other Windows security features.

The release does not create Microsoft Defender exclusions. A security-product detection should be reported with the release version, published checksum, and exact detection name; users should not whitelist an unverified download. The CD-audio helper is built from the auditable managed source in `src/CdAudioPlayer.cs` and `src/Mp3WaveOutPlayer.cs`; its pinned NLayer decoder is MIT-licensed and hash-verified during the build. Music files are passive media and replacing or re-recording them does not change executable malware detections.

## Reporting a vulnerability

Use GitHub's private vulnerability reporting feature for this repository. If that feature is unavailable, open a minimal issue asking the maintainer for a private contact channel; do not post exploit details, personal paths, or secrets publicly.
