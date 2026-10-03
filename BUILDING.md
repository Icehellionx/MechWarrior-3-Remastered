# Maintainer build notes

`build.ps1` is an allowlist-based release builder. It expects the sanitized repository to be a child of the private preservation workspace because that workspace contains locally built and independently obtained inputs that should not be committed here.

Required adjacent workspace inputs:

- extracted official MW3 v1.2 patch payload under `staging/patch12-payload`;
- the pinned UnshieldSharp executable;
- locally built ZipperFixup 0.1.2 static-runtime binaries;
- the MW3-specific DDrawCompat build and shader;
- cdaudio-winmm 0.4.0.3 wrapper files (the auditable managed helper is built from `src/CdAudioPlayer.cs` and `src/Mp3WaveOutPlayer.cs`);
- the tracked, hash-pinned NLayer 1.16.0 NuGet package and MIT license under `third_party/NLayer-1.16.0`;
- redistribution-safe music files;
- maintainer-supplied manual PDFs under the ignored `payload/Manuals` directory; and
- the adjacent canonical WinMM configuration file. The DDrawCompat preset is tracked under `config`.

The build fails if an expected input is missing. It creates a SHA-256 manifest for every embedded file and rejects ISO and `.env` filenames before packaging. The manual PDFs remain ignored source inputs and are distributed only inside the release EXE. Generated payload staging is removed after a successful build.

Build the declared ZipperFixup variant with `& tools/Build-ZipperFixupStatic.ps1`. It requires clean upstream revision `1cf586caf9e959e7c4e17a4ba8b63c96711e91a6`, Rust/Cargo 1.88.0 in the adjacent local toolchain, pinned Cargo.lock and MSVC x64-to-x86 build tools. Override `-VcVarsPath` for another installation. Offline locked builds use `+crt-static` and `/Brepro` in isolated `tools/.build/ZipperFixup-static-crt`; the native repository's ordinary outputs are untouched. `build.ps1` refuses unqualified binary hashes. The qualified MSVC 14.42.34433 outputs remove VC++ redistributable imports; seven patcher tests, the 62-export/ordinal/forwarder comparison and byte-identical real base/PM patch outputs were checked. A compiler/input change requires fresh qualification and reviewed hash updates.

`verify.ps1` extracts the embedded payload from the finished setup executable and exercises the InstallShield extraction, official-patch overlay, and ZipperFixup pipeline against the known development media cabinet. It does not install the game or modify system registry/codec state.

Launcher parity contracts can be run with `& tests/Invoke-LauncherParitySmoke.ps1`. This local suite requires the adjacent qualified r18 wrapper; it checks profile ownership, per-game byte preservation, write failure/lease contention, recovery refusal, diagnostic redaction and clipboard failure, missing documents, dialog reentry/Cancel, and 4:3 geometry. Generated fixtures and rendered dialog images stay in ignored `.local/`. Its 150%/200% launcher layout scaling checks do not replace real Windows DPI or multi-monitor field tests.

Use `build.ps1 -SetupFileName MechWarrior-3-Remastered-Setup-parity-candidate.exe` for the unpublished parity candidate and pass that path explicitly to `verify.ps1 -SetupPath`. Release builds use assembly version 1.2.8; publish a new versioned release after the release gates, preserving prior releases. Settings initially exposes qualified 4x MSAA and the existing off recovery choice; bilinear scaling and 16x texture filtering stay fixed until additional choices pass engine/runtime qualification.

Run `& tests/Invoke-SourceRegression.ps1 -IncludeLauncherParity` on the maintained Windows build machine before packaging. It runs registry, control/save storage, typed shortcuts, recovery, PM media, renderer-evidence and full launcher-parity regressions. The parity suite requires the adjacent qualified r18 input. Reports and compiled tests stay in ignored `.local/source-regression-*`; a failed or incomplete run is never marked passed. Run `& tests/SourceRegressionRunner.Tests.ps1` to check the gate's missing-compiler and compilation-failure reporting. Follow with the existing build and real-media/audio `verify.ps1` gates; these source tests do not replace them. Native static-runtime contracts, root rendering-trace tests and agent-router tests retain their owning commands in `active/TEST_STRATEGY.md`.

Signing strategy: retain an explicitly unsigned candidate until a managed signing service or protected CI certificate is configured and qualified. The README discloses Unknown Publisher/SmartScreen reputation limits. Signing never replaces payload integrity, malware scans or runtime qualification. Do not place signing certificates or private keys in this repository. If code signing is added later, use a managed signing service or protected CI secret and sign inner project binaries before creating the embedded archive, then sign the outer setup executable last.
