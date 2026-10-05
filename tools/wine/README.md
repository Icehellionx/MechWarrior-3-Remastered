# Wine qualification lab

Linux support remains experimental. This record separates successful installer contracts from gameplay qualification; Windows remains the qualified packaging target.

## Automatic mounted-disc selection (v1.2.9, 2026-10-04)

The installer detects Wine using its `ntdll` export, makes the mounted CD-folder picker the primary base-game action and preselects a unique readable mapped drive with the retail `MW3` label and InstallShield cabinets. Wine can classify a directory-backed CD mapping as a fixed drive, so discovery accepts that classification while still requiring the exact label and cabinets. Ambiguous or unavailable discs remain a folder selection. Mount your own CD or ISO/BIN/CUE image with Linux tools and keep its CD-ROM mapping available for base-game play. Setup and launcher do not invoke Windows ISO-mount commands under Wine and do not modify or eject external mounts. Mounted expansion cabinets use the same borrowed-media session; RIP folders remain supported.

The seven-effect base-game adjustment now runs automatically during staging, retaining exact-hash originals and the restoration script. No renderer or audio-backend policy changes. Release setup `332FAF264B88CD4626860DCA473F8F94C4DC435000576AC931BA9F542B090A9D` passes Wine 11/Mono packaged runtime detection, primary-picker geometry, mapped D: automatic selection and fresh both-game Folder staging/finalization, including both leveled sound banks and original-backup hashes. Windows full base ISO/borrowed-folder/PM RIP and real-audio smoke passes. This verifies installation policy; no new gameplay qualification is claimed. Local evidence: `release-1.2.9-policy.log`, `release-1.2.9-install.log` under `.local/wine-lab/reports/`.

Tested environment: Ubuntu 24.04, WineHQ stable 11.0, x64 prefix with x86 support, upstream Wine Mono 10.4.1 (SHA-256 `071f4b2887e1c97a11d791ff3d65be9429eed6dec4c2708888bfd546ba358e23`). Setup is copied inside the prefix rather than loaded from a Windows Docker bind mount. Read-only extracted base disc is mapped as D: CD-ROM; exact MW3 volume label is present. Game media and prefix remain local and are never committed or baked into an image.

`tests/PackagedFolderInstallSmoke.cs` runs actual packaged staging/finalization in a guarded x64 worker (`MW3_WINE_TEST=1`, exact `MW3_TEST_SETUP_SHA256`, optional `MW3_WINE_PM_MEDIA`). Allowed destinations start with `C:\MW3Lab\Wine` and must be empty or preserved saves only. It validates committed game/wrapper hashes, primary shortcut target and absence of staging. Its explicit `--verify` mode inspects an already committed test install. This does not test the interactive media picker or completion dialog.

Wine 11 passes exact candidate Folder installation, both read-only game inputs, typed shortcuts, rendered launcher, packaged uninstall and reinstall preserving an explicitly synthetic storage fixture. Source attributes remain read-only. Windows owning smoke includes read-only PM executable replacement plus media, patching and actual audio output.

Negative controls: Wine 9.0 / Mono 8.1.0 reproduces dynamic WScript CreateShortcut failure while typed Shell Links pass; bundled extraction runtime then fails CoreCLR alignment. A Windows bind-mounted setup can fail Mono image loading; identical bytes copied into the prefix load. These failures do not qualify Wine 9 support.

Historical Docker runtime gates (superseded by the actual VM results below): base game still prompts for CD despite mapped CD-ROM/label. PM requires an MFC42 runtime; using the user's original DLL solely in the disposable prefix passes loader startup. The current launcher reports known loader statuses once rather than retrying graphics. Mesa software rendering permits a deeper PM startup probe, but the game reports video opening failure. Wine defaults to built-in ddraw/winmm; explicit process-local `WINEDLLOVERRIDES="ddraw=n,b;winmm=n,b"` proves packaged wrappers load, without proving gameplay. No global override or renderer substitution is shipped. Xvfb/software GPU and absent physical audio cannot qualify movies, missions, listening, controllers or force feedback. Proton/Steam Deck are separate gates.

Local reports live under `.local/wine-lab/reports`; do not publish media, prefix, personal paths or raw private reports. Production Plan owns remaining qualification.

The 2026-10-03 PM seam-correction candidate (SHA-256 9AFF89394509BB0CFCB0864F5451EC595A6223F5D5B92213F3A8AB6A59B34FD9) also passes exact packaged both-game read-only Folder staging/finalization in a new isolated destination. PM commits PresentationEdgeRepair = 4; game/wrapper hashes, typed shortcut target and staging cleanup pass. Report: wine11-edge-install.log. This is installer evidence, with no new Wine gameplay claim.

## Actual Linux VM follow-up (2026-10-03)

A separate VirtualBox 7.2.20 Ubuntu 24.04 guest now supplements the Docker/Xvfb negative controls. The existing Windows/MW4 lab is preserved. Official Ubuntu amd64 VMDK from https://cloud-images.ubuntu.com/noble/current/ was checked against the published SHA256SUMS: `58001251111AE414947C62799E5B74C522ECAA6FA4CD654F7846042A0C60CBD1`. VM has 4 GB RAM, two vCPUs, VMSVGA with 3D disabled for stable boot, XFCE, HDA output and localhost-only key-authenticated SSH; disks are on a separate drive with sufficient space. Password/root SSH login is disabled. Private keys, seed data, disks, snapshots, media and reports are local evidence, never source-control inputs.

WineHQ stable 11.0 and the same checksum-verified Mono 10.4.1 are installed with both-architecture graphics dependencies. The original base ISO is attached as a real optical device and mounted read-only: Linux `/dev/sr0` reports label MW3; Wine's automatic D:/d:: mapping resolves to the mount and raw device. `wine cmd /c vol D:` succeeds with MW3 and serial 3384-8c68. The cloud kernel required its standard matching Ubuntu linux-modules-extra package for HDA; after loading, ALSA enumerates analog/digital outputs and PulseAudio provides a stereo sink.

The exact 9AFF8939 candidate passes actual packaged both-game Folder staging/finalization using the real DVD plus private read-only PM inputs. Game/wrapper identities, typed shortcut, committed files, PM edge repair and no staging passed. No game runtime claim follows from this result.

A live snapshot stalled and was cancelled through its specifically identified cancellable VirtualBox progress operation. An overlapping base-game probe is inconclusive. The guest subsequently reached poweroff.target with filesystems unmounted; host power-off completed the shutdown. A powered-off baseline snapshot succeeded (`45e249eb-40b8-41c3-97fd-a78b6d697862`). Use powered-off snapshots for this lab. Runtime probes resume after clean boot; bounded process lifetime, loaded-DLL evidence, current logs and unchanged executable/profile/wrapper hashes are required. Virtual GPU limitations must remain distinct from Wine or package failures.
## Experimental gameplay recipe and results

The actual Linux VM rendered two instant missions in each game with Wine built-in graphics and the packaged native audio shim. Both games reached results, wrote real pilot files and closed normally with exit code 0; game/profile/wrapper identities were unchanged and no game/audio helper remained. Both pilots reloaded after restart.  campaign saves, movies and base packaged-launcher gameplay remain unverified. PM packaged-launcher gameplay passed with exit 0 and no recovery retry; bounded launcher cleanup terminated an unresponsive owned Wine audio helper. Both-game wave-output and CD protocol play/pause/resume/stop probes pass. Reports and screenshots remain private under `.local/linux-vm/`.

For the tested direct-game route, enter the installed game's directory in the same prefix and run:

```bash
WINEDLLOVERRIDES="ddraw=b;winmm=n,b" wine ./Mech3fixup.exe
```

Use Wine 11 with x86 support, Mono 10.4.1 and actual 32-bit GL/GLX libraries (including libgl1:i386 and libglx-mesa0:i386, not only Mesa DRI). The base test uses the user's real DVD mounted read-only with both Wine D: mount and d:: raw-device mappings. PM needed the user's original MFC42 runtime inside the private prefix; it is not redistributed. The installer takes its codec from owned game media. The guest required the matching Ubuntu kernel's standard extra modules for HDA audio, and an ordinary XFCE login with the session's own Xauthority.

Native DDrawCompat faults under Wine with both native and built-in winmm; using Wine graphics with native winmm isolates that failure boundary and permits gameplay. No renderer rewrite or global override is shipped. Wine built-in graphics does not use Windows DDrawCompat AA, scaling or edge-repair policy. The guest renderer is software llvmpipe, so these results do not qualify hardware-accelerated Linux, controller/FFB, Proton or Steam Deck support. Windows remains the qualified packaging target.


For the tested PM launcher route, use the same override with wine ./MW3Launcher.exe from the install root. Launcher-to-PM inheritance, saved-pilot selection and a rendered mission passed. Base launcher media handling remains a separate check.

Linux lab final state (2026-10-03): guest shut down cleanly after DVD unmount, powered-off snapshot 76476173-ac3f-4e3e-b5cb-807d7a41f3a2 preserves the current 3D-off configuration, 32-bit GL, audio modules and test results. Earlier snapshot 45e249eb predates these corrections.
