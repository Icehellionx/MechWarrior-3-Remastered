# MechWarrior 3 Remastered

An unofficial community installer for running the 1999 PC release of MechWarrior 3 on modern Windows. It extracts the game from media you provide, applies the official v1.2 update, and installs the compatibility and presentation fixes used by this preservation project.

This project is not affiliated with or endorsed by MicroProse, Hasbro Interactive, Zipper Interactive, Microsoft, The Topps Company, or any current rights holder.

## ⬇️ Download the installer

### [Download MechWarrior 3 Remastered v1.2.6 Setup.exe](https://github.com/Icehellionx/MechWarrior-3-Remastered/releases/latest/download/MechWarrior-3-Remastered-Setup.exe)

Current release: [v1.2.6](https://github.com/Icehellionx/MechWarrior-3-Remastered/releases/tag/v1.2.6)

Renderer source and standalone package:
[DDrawCompat-MW3](https://github.com/Icehellionx/DDrawCompat-MW3). This is the
public MW3-specific fork used by the remaster for graphics compatibility,
including 32-bit render-color-depth promotion and the Pirate's Moon startup
surface recovery.

**Most people only need the installer link above.** You supply your own MechWarrior 3 ISO when the installer asks for it. Pirate's Moon is optional and accepts the commonly available RIP ZIP/folder, an original ISO, or an ISO-version ZIP containing a mixed-mode BIN/CUE image.

1. Download and run the installer.
2. Point it to your MechWarrior 3 ISO.
3. Open the new desktop launcher and choose a game or manual.

If the direct link does not work, open the [latest release page](https://github.com/Icehellionx/MechWarrior-3-Remastered/releases/latest) and download `MechWarrior-3-Remastered-Setup.exe` under **Assets**.

## Windows warning and checksum

For verification, download [`SHA256SUMS.txt`](https://github.com/Icehellionx/MechWarrior-3-Remastered/releases/latest/download/SHA256SUMS.txt) from the same release. Do not download reposted copies from unrelated websites.

The installer is currently unsigned. Windows may display **Unknown publisher** or a Microsoft Defender SmartScreen warning. Verify the SHA-256 digest before deciding whether to run it; never disable SmartScreen or antivirus protection globally for this project.

```powershell
Get-FileHash .\MechWarrior-3-Remastered-Setup.exe -Algorithm SHA256
Get-Content .\SHA256SUMS.txt
```

The two values should match exactly.

## What you need

- Windows 10 or Windows 11 on an x64 PC with .NET Framework 4.7.2 or later.
- A legally obtained US MechWarrior 3 ISO.
- Optionally, the commonly distributed Pirate's Moon RIP ZIP/extracted folder, a legally obtained US ISO, or an ISO-version ZIP containing its BIN/CUE image.
- About 1 GB of free space for MW3, plus additional space if installing Pirate's Moon.

Neither ISO is included, uploaded, or copied into the installed directory.

## Installation

1. Run the setup EXE.
2. Browse to your MechWarrior 3 ISO.
3. Optionally select Pirate's Moon and browse to its RIP ZIP, extracted RIP folder, ISO, or ISO-version BIN/CUE ZIP.
4. Keep the default per-user `LocalAppData\Programs` destination or choose another empty directory.
5. Use the installed desktop or Start-menu launcher to open either game or either manual.

Keep the MechWarrior 3 ISO at the path selected during setup. Pirate's Moon media is needed only during installation; setup converts the verified US retail executable from an ISO to the same hash-verified no-disc runtime used by the supported RIP path.

## What setup changes

- Extracts the retail game from the selected ISO.
- Converts either verified Pirate's Moon retail executable variant to the project's single verified no-disc runtime, leaving selected media unchanged. The true disc executable receives its 12-byte runtime-path normalization plus the two disc-check branch changes; the partly normalized RIP retail executable needs only the two branch changes. ISO-version ZIPs are safely expanded and their Mode-1 BIN/CUE ISO 9660 data volume is converted to a temporary mountable ISO; the audio track is not copied because the remaster supplies its own redistribution-safe music.
- Restores the small `DATA.TAG` application marker that the original Pirate's Moon InstallShield setup creates but cabinet-only extraction omits; without it, ISO-derived installations can terminate with an access violation during startup.
- Applies the official MechWarrior 3 v1.2 files.
- Adds DDrawCompat, ZipperFixup, the MW3 remaster shader, and CD-audio compatibility.
- Creates one all-user desktop and Start-menu shortcut that opens the branded game-selection launcher with no direct-game argument. Setup removes only recognized legacy project shortcuts that bypassed this launcher.
- Installs a black-and-red remaster hub for both games and both PDF manuals.
- Starts with desktop-resolution internal rendering and 4× MSAA. If Pirate's Moon loses its primary DirectDraw surface during the borderless startup transition, the compatibility wrapper restores it and retries the failed attachment in-process before the launcher advances through its five ordered recovery profiles.
- Keeps the legacy 640×480 startup-video mode available alongside the tested 1024×768 gameplay mode; DDrawCompat scales gameplay to the desktop resolution. Changing resolution from the legacy in-game menu remains unsupported because it can crash the game.
- Creates and checks writable `keys` storage so keyboard, mouse, joystick/HOTAS mappings can be saved without altering existing profiles.
- Restores the known-good Direct3D adapter and video-mode values before each recovery attempt and records launch diagnostics under `%LOCALAPPDATA%\MechWarrior 3 Remastered\launcher.log`.
- Starts CD music at 60% on first launch and preserves later in-game volume changes. MP3 decoding uses the pinned MIT-licensed NLayer decoder and Windows `waveOut`, avoiding the legacy MCI MP3 driver. The audio helper shuts down with the game, including when the launcher is interrupted, so it does not retain installation-file locks.
- Registers an uninstaller in Windows Apps/Installed apps.
- Provides an **UNINSTALL** action in the launcher's bottom-right corner. Uninstall removes the installed games, compatibility files, settings, diagnostics, and shortcuts while retaining each game's `pilots` folder. Reinstalling to the same location recognizes that save-only remainder and restores the saved pilots and campaign progress into the new staged installation.
- Installs the hash-verified Indeo 5 codec from the user's MW3 disc for retail FMV playback.
- Stores the selected MechWarrior 3 ISO path locally in `install.cfg`.
- Waits for the mounted MechWarrior 3 ISO to receive a drive letter and become readable before starting the base game, and confirms that launcher-owned media is actually detached after use before reporting a successful eject.
- Installs both original manuals under `Manuals`, links them from the launcher, and also links them from the Start menu.
- Registers the games' complete-install metadata in the canonical 32-bit Windows registry view, preventing the misleading `Software Render Files component was not installed during Setup` startup error on clean systems.

See [SECURITY.md](SECURITY.md), [PRIVACY.md](PRIVACY.md), and [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) before installing if you want the full details.

## Known limitations

- The release is not Authenticode-signed, so Windows cannot display a verified publisher.
- The US MW3 disc, common Pirate's Moon RIP folder/ZIP, and `MechWarrior-3-Pirates-Moon_Win_EN_ISO-Version.zip` BIN/CUE paths have passed extraction and patching tests. The ISO-version archive test covers safe expansion, Mode-1 conversion, mount/eject, InstallShield extraction, the true disc-executable transformation, compatibility installation, and the shared installed-game/audio contract.
- This project does not promise compatibility with other regions or modified disc images.

## Reporting a bug

Use the [guided bug report form](https://github.com/Icehellionx/MechWarrior-3-Remastered/issues/new?template=bug_report.yml) and include the requested launcher diagnostics. Before uploading, remove personal paths or other private information. Never attach game ISOs, extracted game files, credentials, or unrelated crash artifacts.

## Development

The installer and launcher source is under `src`. The maintainer build script assembles an allowlisted payload from the adjacent preservation workspace, rejects ISO and environment files, compiles the launchers, embeds the payload, and generates release hashes.

```powershell
& .\build.ps1
& .\verify.ps1
& .\verify.ps1 -Mw3IsoPath "C:\path\to\MechWarrior 3.iso"
& .\verify.ps1 -PiratesMoonIsoPath "C:\path\to\Pirate's Moon.iso" -VerifyPiratesMoonRip
& .\verify.ps1 -VerifyAudioOutput
```

Both ISO parameters use the same mount, extraction, compatibility, installed-tree, input, renderer, audio, and cleanup contract; the base disc additionally requires populated video copying. Game-specific steps are limited to the official v1.2 patch for the base game and the verified retail-to-no-disc transformation for Pirate's Moon. `-PiratesMoonArchivePath` adds safe ZIP and BIN/CUE conversion ahead of that same Pirate's Moon contract.
`-VerifyAudioOutput` is an interactive-machine gate that decodes and briefly plays an installed MP3 through the same 32-bit NLayer/`waveOut` path used by the CD-audio helper. It verifies direct output plus the installed helper's play/pause/resume/stop protocol. Keep it separate from headless protocol tests because CI or sandbox accounts may not expose a usable output device.

The build script intentionally does not download dependencies. A public clone therefore needs the pinned third-party inputs and redistribution-safe assets described in [BUILDING.md](BUILDING.md). Release binaries are published separately on GitHub Releases rather than committed to Git history.

## License

The original installer and launcher source in this repository is available under the [MIT License](LICENSE). That license does not grant rights to MechWarrior, the retail game, official patch, music, trademarks, or third-party components. Each third-party component remains governed by its own terms.
