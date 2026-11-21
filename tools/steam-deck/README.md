# MechWarrior 3 Remastered Steam Deck Installation Variant

This alpha packages the Windows 1.2.9 Setup with an opt-in Steam launch script. It uses a separate Proton prefix and Wine’s built-in DirectDraw with the packaged WinMM audio shim. The Ubuntu Wine 11 lab rendered missions in both the base game and Pirate’s Moon with this combination. Windows DDrawCompat enhancements are unavailable in this mode. SteamOS Game Mode, Deck controls and accelerated graphics are open for feedback.

## Install in Desktop Mode

1. Extract the entire ZIP into `/home/deck/Games/MW3-Deck-Alpha`. In a terminal in that folder, run `sha256sum -c SHA256SUMS.txt` to verify the files.
2. Install the x86-64 build of [GE-Proton11-12](https://github.com/GloriousEggroll/proton-ge-custom/releases/tag/GE-Proton11-12) using the [upstream Steam installation instructions](https://github.com/GloriousEggroll/proton-ge-custom/blob/GE-Proton11-12/README.md#installation). Restart Steam.
3. Add `MechWarrior-3-Remastered-Setup.exe` as a non-Steam game. Under **Compatibility**, force GE-Proton11-12. Under **General → Launch Options**, paste:

   ```text
   bash "/home/deck/Games/MW3-Deck-Alpha/launch.sh" setup %command%
   ```

   Adjust the script path if you extracted the ZIP elsewhere. Keep `%command%` at the end.
4. Start Setup once to create the alpha prefix. The base game expects its original CD at runtime. Mount your own CD or ISO read-only in Desktop Mode, then [map the disc in the alpha prefix](#map-the-original-base-game-disc). Open Setup again, use **CD folder…** if the disc is not preselected, and choose `C:\MechWarrior3-Deck-Alpha` as the destination. Supply your own Pirate’s Moon media if you want the expansion.
5. In Steam, add the installed launcher as a second non-Steam game. Its alpha path for the destination above is:

   ```text
   /home/deck/.local/share/mw3-remastered-deck-alpha/pfx/drive_c/MechWarrior3-Deck-Alpha/MW3Launcher.exe
   ```

   Dolphin shows hidden folders with **Ctrl+H**. Force the same GE-Proton version on this shortcut and use these launch options:

   ```text
   bash "/home/deck/Games/MW3-Deck-Alpha/launch.sh" play %command%
   ```

## Map the original base-game disc

Add `/home/deck/.local/share/mw3-remastered-deck-alpha/pfx/drive_c/windows/system32/winecfg.exe` as a temporary non-Steam shortcut. Use the same GE-Proton version and the same `setup` launch options. In **Drives**, select a free drive letter and set its path to the mounted disc (for example, `Z:\run\media\deck\MW3`, if that is where it is mounted). Under **Show Advanced**, set the type to **CD-ROM**. Apply the change and close Wine configuration. Keep the disc mounted at the same path while playing. If the game still asks for the CD, include the media and mapping details in your report.

## Play and report

Start the launcher in Desktop Mode for the first run. A mouse and keyboard or a Steam Input layout that maps to them can help navigate the Windows launcher, pilot screens and game. Try Game Mode after a successful desktop launch.

In an [issue report](https://github.com/Icehellionx/MechWarrior-3-Remastered/issues/new), include the Deck model, SteamOS and Proton versions, base game or expansion, disc type, and last screen that worked. Include audio and controller results from a mission. To collect a Proton log, add `--log` after `setup` or `play`; logs go in `~/.local/share/mw3-remastered-deck-alpha/logs`. Review logs for private paths and usernames before posting. Keep game media, installed game files and saves private.

The shortcuts share the alpha prefix. Keep that directory if you want to retain installed games, their registry settings and saves. Changes to graphics and audio apply only to the play shortcut. Use the normal Windows download for Windows.

## Build the alpha package

Run `python tools/steam-deck/build-alpha.py SETUP.exe SETUP_SHA256 MechWarrior-3-Steam-Deck-Alpha.zip` against the Windows 1.2.9 Setup and its published SHA-256. This packages the exact Setup bytes with this guide, the launch script and a checksum manifest. The original game discs are supplied by the user.
