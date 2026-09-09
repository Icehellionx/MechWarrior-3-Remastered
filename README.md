# MechWarrior 3 Remastered

An unofficial community installer for running the 1999 PC release of MechWarrior 3 on modern Windows. It extracts the game from media you provide, applies the official v1.2 update, and installs the compatibility and presentation fixes used by this preservation project.

This project is not affiliated with or endorsed by MicroProse, Hasbro Interactive, Zipper Interactive, Microsoft, The Topps Company, or any current rights holder.

## Download

Download `MechWarrior-3-Remastered-Setup.exe` and `SHA256SUMS.txt` together from the repository's **Releases** page. Do not download reposted copies from unrelated websites.

The installer is currently unsigned. Windows may display **Unknown publisher** or a Microsoft Defender SmartScreen warning. Verify the SHA-256 digest before deciding whether to run it; never disable SmartScreen or antivirus protection globally for this project.

```powershell
Get-FileHash .\MechWarrior-3-Remastered-Setup.exe -Algorithm SHA256
Get-Content .\SHA256SUMS.txt
```

The two values should match exactly.

## What you need

- Windows 10 or Windows 11 on an x64 PC.
- A legally obtained US MechWarrior 3 ISO.
- Optionally, a legally obtained US Pirate's Moon ISO.
- About 1 GB of free space for MW3, plus additional space if installing Pirate's Moon.

Neither ISO is included, uploaded, or copied into the installed directory.

## Installation

1. Run the setup EXE.
2. Browse to your MechWarrior 3 ISO.
3. Optionally select Pirate's Moon and browse to its ISO.
4. Keep the default `Program Files (x86)` destination or choose another empty directory.
5. Use the installed desktop or Start-menu shortcut.

Keep the ISO files at the paths selected during setup. The launcher mounts the relevant disc before starting the game and prompts again if the file has moved.

## What setup changes

- Extracts the retail game from the selected ISO.
- Applies the official MechWarrior 3 v1.2 files.
- Adds DDrawCompat, ZipperFixup, the MW3 remaster shader, and CD-audio compatibility.
- Creates all-user desktop and Start-menu shortcuts.
- Registers an uninstaller in Windows Apps/Installed apps.
- Installs the hash-verified Indeo 5 codec from the user's MW3 disc for retail FMV playback.
- Stores the selected ISO paths locally in `install.cfg`.

See [SECURITY.md](SECURITY.md), [PRIVACY.md](PRIVACY.md), and [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) before installing if you want the full details.

## Known limitations

- The release is not Authenticode-signed, so Windows cannot display a verified publisher.
- The US MW3 disc path has passed the complete extraction and patching smoke test. Pirate's Moon executable patching has been verified, but a complete expansion ISO was not available for an end-to-end media test.
- This project does not promise compatibility with other regions or modified disc images.

## Development

The installer and launcher source is under `src`. The maintainer build script assembles an allowlisted payload from the adjacent preservation workspace, rejects ISO and environment files, compiles the launchers, embeds the payload, and generates release hashes.

```powershell
& .\build.ps1
& .\verify.ps1
```

The build script intentionally does not download dependencies. A public clone therefore needs the pinned third-party inputs and redistribution-safe assets described in [BUILDING.md](BUILDING.md). Release binaries are published separately on GitHub Releases rather than committed to Git history.

## License

The original installer and launcher source in this repository is available under the [MIT License](LICENSE). That license does not grant rights to MechWarrior, the retail game, official patch, music, trademarks, or third-party components. Each third-party component remains governed by its own terms.
