# Third-party notices and credits

This is an unofficial community preservation installer. It is not affiliated with or endorsed by MicroProse, Hasbro Interactive, Zipper Interactive, Microsoft, or any current rights holder. MechWarrior, BattleTech, and related marks belong to their respective owners. The installer does not contain either game ISO; users must supply their own legally obtained media.

The installer and launcher glue are original work prepared for this project. Compatibility components and extraction tools are credited below. Their retained license/readme files are installed in `Third-Party`.

- **DDrawCompat** by Narzoul and contributors — <https://github.com/narzoul/DDrawCompat>. The bundled project build is based on v0.7.1 plus this project's MW3-specific presentation/remaster changes. BSD Zero Clause License.
- **ZipperFixup** by Terran Mechworks contributors — <https://github.com/TerranMechworks/ZipperFixup>. Bundled build: v0.1.2. European Union Public Licence 1.2.
- **cdaudio-winmm** by dippy-dipper/DD and contributors — <https://github.com/dippy-dipper/cdaudio-winmm>. Its WinMM wrapper retains the copyright and permissive license notice of Toni Spets; later player code is credited to DD. Bundled base: v0.4.0.3; the helper was rebuilt by this project to run headlessly and follow the game process lifetime.
- **UnshieldSharp** by Michael N. Manger and SabreTools contributors — <https://github.com/mnadareski/UnshieldSharp>. Used only during setup to extract the legacy InstallShield cabinets. MIT License.
- The official **MechWarrior 3 v1.2 patch** is redistributed as an official compatibility update under the permission represented by the project owner. Its original readme is retained with the installed game.
- The project maintainer represents that the bundled music files are redistribution-safe recordings. No audio is extracted from or copied out of the selected ISO by the installer. See `REDISTRIBUTION.md` for the scope of that representation.
- The original MechWarrior 3 and Pirate's Moon manuals are bundled at the maintainer's direction for player reference. They remain the property of their respective rights holders and are not relicensed by this project.

No game code or binary from the Pirate's Moon RIP or its crack is embedded in this installer. Setup can read the known RIP ZIP/folder selected by the user, verify its executables, and install only the required game payload. ISO-based installations continue to require the ISO when launched; RIP-based installations do not.
