# Maintainer build notes

`build.ps1` is an allowlist-based release builder. It expects the sanitized repository to be a child of the private preservation workspace because that workspace contains locally built and independently obtained inputs that should not be committed here.

Required adjacent workspace inputs:

- extracted official MW3 v1.2 patch payload under `staging/patch12-payload`;
- the pinned UnshieldSharp executable;
- locally built ZipperFixup 0.1.2 binaries;
- the MW3-specific DDrawCompat build and shader;
- cdaudio-winmm 0.4.0.3 files and the project's headless helper build;
- redistribution-safe music files; and
- canonical DDrawCompat and WinMM configuration files.

The build fails if an expected input is missing. It creates a SHA-256 manifest for every embedded file and rejects ISO and `.env` filenames before packaging. Generated payload staging is removed after a successful build.

`verify.ps1` extracts the embedded payload from the finished setup executable and exercises the InstallShield extraction, official-patch overlay, and ZipperFixup pipeline against the known development media cabinet. It does not install the game or modify system registry/codec state.

Do not place signing certificates or private keys in this repository. If code signing is added later, use a managed signing service or protected CI secret and sign inner project binaries before creating the embedded archive, then sign the outer setup executable last.
