# Local rollback markers

`MechWarrior-3-Remastered-Setup-r3-mouse-safe.exe` is the complete installer
candidate preserved immediately before enabling the optional INTRO.AVI chroma
cleanup experiment.

- SHA-256: `360809F06ED4F8E94081F6FAFCBF499487ED7AD16BE7A0BAF59DEF8F61D51AD0`
- Renderer DLL SHA-256: `17EFC125BC46E6C472D354F8A93C6AC05C68B3D4211E94417F4B3CC950D4F877`
- Status: build, installer smoke verification, and Defender scans passed; actual
  graphics behavior still required field testing.

For a filter-only rollback, set `RemasterIntroChromaCleanup = off` in the
installed `DDrawCompat.ini`. The archived installer is the full-package fallback.

`MechWarrior-3-Remastered-Setup-r4-intro-chroma.exe` preserves the next tested
checkpoint, immediately before adding click-to-reactivate behavior to the
inactive presentation window.

- SHA-256: `7D9F5B9CEDED3549513D3D51AF1F81AD04DBE335500BEECF31B622F4B53C1A3E`

`MechWarrior-3-Remastered-Setup-r5-click-reactivate.exe` preserves the click
reactivation candidate immediately before the launcher audio-helper lifetime
fix.

- SHA-256: `0903BFA8AB753C1CC21DBD8539D393EBA09BED2E1A2637509AD22B2E1FFAAAA6`

`MechWarrior-3-Remastered-Setup-r5-audio-lifecycle.exe` preserves the
click-reactivation and audio-helper lifetime fixes immediately before the
one-shot reactivation cursor correction.

- SHA-256: `4E2D058E1E40D3F26385AB06692A6AF9649B9CDA80FED765F39879EE4536EA34`

`MechWarrior-3-Remastered-Setup-r6-cursor-reactivate.exe` preserves the
one-shot cursor correction immediately before moving system-cursor suppression
to the active game client's `WM_SETCURSOR` event.

- SHA-256: `B5E13237C08CCE618B244720E8A3D31B2B78185400EB5763C094D42D1720E9B1`

`MechWarrior-3-Remastered-Setup-r7-active-cursor-hide.exe` preserves the
message-only cursor fix immediately before the active/inactive cursor state was
moved into DDrawCompat's emulated-cursor boundary.

- SHA-256: `473A7D1F466B65EB2ABC847FE1C351773B8677C8DECDEACD240362BE6BF757A2`

`MechWarrior-3-Remastered-Setup-r8-cursor-state.exe` preserves the persistent
cursor-state fix immediately before adding the cursor-free presentation repaint.

- SHA-256: `FA3CCDCC061CAAB28FA87DAA80BF51BC17E30773345F35A28B26D77AF7068DC1`

`MechWarrior-3-Remastered-Setup-r9-cursor-repaint.exe` preserves the
cursor-state/repaint candidate immediately before clearing the cursor on the
presentation GUI thread that owns it.

- SHA-256: `B7C429BBB607C4F43FBF97BA4574BCBC785413B1CF253D65D3913BD4778ABABE`

`MechWarrior-3-Remastered-Setup-r10-gui-cursor-clear.exe` preserves the
presentation-thread cursor-clear candidate immediately before the final
presentation renderer was taught to honor the active-game cursor-hidden state.

- SHA-256: `56052ABFBDD039AB502A44F298610464E71230AC29BD23D89E2D8EA840893F99`

`MechWarrior-3-Remastered-Setup-r11-render-cursor-gate.exe` preserves the
render-time cursor-gate candidate immediately before enabling synchronized
presentation for Pirate's Moon only.

- SHA-256: `E822A09F61C29085553C1495A7C79B5CCF89ED7994A6A88C9E75A21E767B8ABD`

`MechWarrior-3-Remastered-Setup-r12-pm-vsync.exe` preserves the Pirate's-Moon
VSync candidate immediately before the no-MSAA diagnostic experiment. That
experiment later failed identically on attempt 1, so the current candidate
restores 4× MSAA and internal upscaling.

- SHA-256: `46963545994B528FCFD60C4BC011FAD057FEA2DD27518C3B2682DB395FB63D4F`

`MechWarrior-3-Remastered-Setup-r16-pm-startup-black.exe` preserves the
disproven Pirate's-Moon startup-surface-clear experiment before it was replaced
with standard DDrawCompat partial-frame coalescing.

- SHA-256: `2C8CF528A60F0C34B7B717284078276A8F6D726DA3A17FCA2C6510E1B40EBC7F`
