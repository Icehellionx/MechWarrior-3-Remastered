# Local sound-effect level candidate

This is the source for setup's optional experimental change to unusually loud MechWarrior 3 sound effects. It changes only copied PCM samples from the exact supported US v1.2 `soundsH.zbd` and `soundsL.zbd` archives. The original disc, extracted source, music helper, and Pirate's Moon files remain unchanged. Generated ZBD files contain user-owned game audio: keep them local and do not commit or redistribute them.

Build the leveler with the installed .NET Framework compiler, then point it at the original base game's `zbd` folder and an output folder outside the game:

```powershell
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$toolDir = Join-Path $env:TEMP 'mw3-sound-level-tool'
New-Item -ItemType Directory -Path $toolDir -Force | Out-Null
$leveler = Join-Path $toolDir 'SoundArchiveLeveler.exe'
& $csc /nologo /target:exe /define:SOUND_LEVEL_TOOL /platform:x64 /optimize+ "/out:$leveler" .\src\SoundArchiveLeveler.cs
& $leveler 'C:\path\to\original-game\zbd' 'C:\path\to\local-candidate'
```

The tool rejects any source archive whose hash differs from the reviewed inputs. Candidate hashes must be `20AD72D51EAFFB4447A7CE09B408B017CFAA5A7034A82E73B85B539355579BCB` and `612F8EDB1E26884AAD04E34F3E73D41D7661F8652F97EB765C8C28EAC9A37D98`. It does not overwrite a differing candidate already in the output folder.

With the game closed, apply or restore both banks using the hash-checked script. It saves byte-identical originals in `OriginalSoundArchives` under the selected game folder:

```powershell
& .\tools\Use-SoundLevelCandidate.ps1 -GameRoot 'C:\path\to\installed\MechWarrior 3' -CandidateRoot 'C:\path\to\local-candidate'
& .\tools\Use-SoundLevelCandidate.ps1 -GameRoot 'C:\path\to\installed\MechWarrior 3' -Restore
```

This only reduces the level of seven named effects. It cannot repair clipping already baked into the original WAV samples, and it has not passed an in-game listening test. See `../active/SOUND_EFFECT_TRACE.md` in the workspace for measurements and event tracing.
