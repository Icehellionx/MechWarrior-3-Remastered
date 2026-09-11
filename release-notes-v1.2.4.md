# MechWarrior 3 Remastered v1.2.4

This update adds adaptive recovery and restores the previously qualified graphics wrapper after field testing found that the experimental renderer could fail all four initialization attempts.

## Changes

- The release wrapper is derived directly from qualified DDrawCompat v0.7.1 and preserves the field baseline as one unit: isolated `INTRO.AVI` crop/matte cleanup, output-only top/left gameplay edge repair, and composable borderless Alt-Tab behavior. A separately switchable `INTRO.AVI`-only shader now smooths low-level dark chroma noise without applying a black clamp or altering gameplay surfaces. The broad experimental shader, telemetry, and texture hooks are not shipped.
- Set `RemasterIntroChromaCleanup = off` in `DDrawCompat.ini` to disable only the new polish pass. Recovery attempts 2 through 5 also force it off, and the complete mouse-safe r3 installer remains archived as a full rollback marker.
- The launcher's shipped profile retains desktop-resolution internal rendering and 4× MSAA. A no-MSAA field build failed on attempt 1 in the same way, disproving MSAA as the cause. The retained Pirate's Moon attempt-1 log identified the fatal race precisely: `IDirectDrawSurface4::GetAttachedSurface` returned `DDERR_SURFACELOST` while borderless presentation was changing fullscreen mode and rebuilding resources. The compatibility wrapper now restores and retries lost primary-surface `GetAttachedSurface` and `AddAttachedSurface` calls in-process on a bounded schedule, followed by the existing clipper and Direct3D guards; successful calls are unchanged. The intro is not skipped or replaced. Reduced-feature and native-scale profiles remain later safety fallbacks.
- Recovery settings are written only to a temporary process-specific DDrawCompat configuration. A disk-backed recovery record restores any existing player configuration byte-for-byte after launch or after an interrupted launcher process, and removes the temporary file when no prior file existed.
- Process checks are scoped to this installation. The launcher no longer blocks on or terminates similarly named game/audio processes from unrelated folders.
- ISO media is attached only while it is needed. Setup ejects installer-owned media immediately after extraction; the launcher ejects launcher-owned media when the game exits or recovery fails. Media already mounted by the player is reused and deliberately left attached.
- A cross-process launch lease prevents overlapping launcher/recovery loops from competing for the same game and ISO. Mount ownership is persisted so the next run can clean up after an interrupted launcher.
- Installation-owned CD-audio helpers now receive their supported graceful shutdown command before any kill fallback. Cleanup runs on every launcher exit path and before uninstall. The helper also watches the process that launched it and exits itself when that game process ends, covering launcher crashes or termination. This prevents an orphaned helper from locking the installation directory; similarly named helpers outside this installation remain untouched.
- Early nonzero process exits, including access violations, remain failed recovery attempts instead of being reported as successful launches.
- Borderless `keepvidmem(1)` Alt-Tab preserves the visible game frame without minimizing, releases desktop cursor capture, stays in the normal window band so other applications and capture overlays remain visible, and restores fullscreen input on reactivation.
- Window z-order is changed only after a real deactivation event; startup remains untouched so intro-video initialization is not disrupted.
- Pirate's Moon now has its own lean DDrawCompat configuration with standard 50 ms `PresentDelay` and VSync enabled. Base-game-only intro crop/chroma and gameplay-edge settings are not injected into the expansion. The prior VSync-wait and startup-surface-clear experiments did not solve the underlying initialization race and are not packaged.
- The inactive presentation layer accepts a click only to reactivate the real game window, consumes that click so it cannot reach the desktop or game, then returns to its disabled/click-through state. Active MW3 state owns cursor visibility at DDrawCompat's emulation boundary, forces a cursor-free repaint, clears the presentation GUI thread's cursor at the handoff, and prevents the final presentation renderer from compositing the remembered desktop arrow into the low-resolution game surface. Deactivation restores ordinary cursor presentation for the next foreground application. It does not manipulate the Windows cursor counter or run polling/capture loops.
- New installations default to the current user's local Programs directory, where the legacy game and launcher can maintain runtime configuration without elevation. A system-wide Program Files location can prevent adaptive profiles and diagnostics from being written.
- `mech3.out` and the DDrawCompat log are archived for every attempt under `%LOCALAPPDATA%\MechWarrior 3 Remastered\attempts` so earlier evidence is no longer overwritten by later retries.
- The launcher log now records the launcher version and the recovery profile used by each attempt.
- The launcher manifest declares current Windows compatibility, preventing modern Windows releases from being logged as Windows NT 6.2.

## Verification

The updated launcher compiles successfully with the release build settings, and the manifest parses as valid XML. Focused recovery tests cover byte-preserving restore, no-original-file cleanup, restart recovery after interruption, owned-versus-unrelated process paths, and ISO mount-ownership classification.

The final r18 wrapper passed five standard launcher attempt-1 tests, including a fresh-path run; three tests encountered and recovered the formerly fatal surface-loss race. The exact installer-extracted Pirate's Moon tree then passed three direct launches with no launcher retry or fallback available. Captures from all three direct launches showed a clean black MicroProse splash and intact logo without the uniform gray failure frame.

The finished setup, its extracted 62-file payload, and the exact runnable Pirate's Moon tree passed Microsoft Defender custom scans. The system reported 11 detections before and 11 after the scans, so the package produced no new malware detection.

Setup SHA-256: `BF6D0AD21DCBD3114D8D5A4F09DADE1984CF7528EDEE98633463648EE833E068`.

Packaged DDrawCompat SHA-256: `FD11B9B6B8A8CC23744DFEDC10E23798860F3A96BD8B2B4C75DD2FDB3BE8F8FB`.

Pre-no-MSAA experiment installer SHA-256: `46963545994B528FCFD60C4BC011FAD057FEA2DD27518C3B2682DB395FB63D4F`.

Pre-Pirate's-Moon-VSync installer SHA-256: `E822A09F61C29085553C1495A7C79B5CCF89ED7994A6A88C9E75A21E767B8ABD`.

Pre-render-time cursor gate installer SHA-256: `56052ABFBDD039AB502A44F298610464E71230AC29BD23D89E2D8EA840893F99`.

Pre-presentation-thread cursor fix installer SHA-256: `B7C429BBB607C4F43FBF97BA4574BCBC785413B1CF253D65D3913BD4778ABABE`.

Pre-cursor-repaint installer SHA-256: `FA3CCDCC061CAAB28FA87DAA80BF51BC17E30773345F35A28B26D77AF7068DC1`.

Pre-emulation-boundary cursor fix installer SHA-256: `473A7D1F466B65EB2ABC847FE1C351773B8677C8DECDEACD240362BE6BF757A2`.

Pre-event-driven cursor fix installer SHA-256: `B5E13237C08CCE618B244720E8A3D31B2B78185400EB5763C094D42D1720E9B1`.

Pre-cursor-correction installer SHA-256: `4E2D058E1E40D3F26385AB06692A6AF9649B9CDA80FED765F39879EE4536EA34`.

Known-good r4 rollback installer SHA-256: `7D9F5B9CEDED3549513D3D51AF1F81AD04DBE335500BEECF31B622F4B53C1A3E`.

Pre-lifetime-fix r5 rollback installer SHA-256: `0903BFA8AB753C1CC21DBD8539D393EBA09BED2E1A2637509AD22B2E1FFAAAA6`.

Known-good r3 rollback installer SHA-256: `360809F06ED4F8E94081F6FAFCBF499487ED7AD16BE7A0BAF59DEF8F61D51AD0`.

The installer remains unsigned, so Windows may display an Unknown publisher or SmartScreen warning.
