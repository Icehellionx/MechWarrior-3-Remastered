[CmdletBinding()]
param(
    [switch]$KeepSmokeTree,
    [switch]$VerifyPiratesMoonRip,
    [switch]$VerifyAudioOutput,
    [string]$SetupPath,
    [string]$Mw3IsoPath,
    [string]$PiratesMoonIsoPath,
    [string]$PiratesMoonArchivePath
)

$ErrorActionPreference = 'Stop'
$releaseRoot = $PSScriptRoot
. (Join-Path $releaseRoot 'tests\InstallerGameSmokeContract.ps1')
$smoke = Join-Path $releaseRoot 'smoke-test'
if (Test-Path -LiteralPath $smoke) {
    $resolved = (Resolve-Path -LiteralPath $smoke).Path
    if (-not $resolved.StartsWith($releaseRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe smoke-test path.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
New-Item -ItemType Directory -Path $smoke | Out-Null

try {
    $setup = if ($SetupPath) { (Resolve-Path $SetupPath).Path } else { (Resolve-Path (Join-Path $releaseRoot 'dist\MechWarrior-3-Remastered-Setup.exe')).Path }
    $assembly = [Reflection.Assembly]::LoadFile($setup)
    $type = $assembly.GetType('InstallerForm')
    $flags = [Reflection.BindingFlags]'NonPublic,Static'
    $payload = [string](Join-Path $smoke 'payload')
    $game = [string](Join-Path $smoke 'game')
    New-Item -ItemType Directory -Path $payload | Out-Null
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    $netStandard = Get-ChildItem -LiteralPath 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework' -Filter netstandard.dll -Recurse |
        Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
    $registryTest = Join-Path $smoke 'GameInstallRegistrySmoke.exe'
    & $csc /nologo /target:exe /platform:anycpu /optimize+ "/out:$registryTest" (Join-Path $releaseRoot 'tests\GameInstallRegistrySmoke.cs') (Join-Path $releaseRoot 'src\GameInstallRegistry.cs')
    if ($LASTEXITCODE) { throw "Game install registry test compilation failed with exit code $LASTEXITCODE." }
    & $registryTest
    if ($LASTEXITCODE) { throw "Game install registry tests failed with exit code $LASTEXITCODE." }
    $controlStorageTest = Join-Path $smoke 'GameControlStorageSmoke.exe'
    & $csc /nologo /target:exe /platform:anycpu /optimize+ "/out:$controlStorageTest" (Join-Path $releaseRoot 'tests\GameControlStorageSmoke.cs') (Join-Path $releaseRoot 'src\GameControlStorage.cs')
    if ($LASTEXITCODE) { throw "Control-profile storage test compilation failed with exit code $LASTEXITCODE." }
    & $controlStorageTest
    if ($LASTEXITCODE) { throw "Control-profile storage tests failed with exit code $LASTEXITCODE." }
    $saveStorageTest = Join-Path $smoke 'GameSaveStorageSmoke.exe'
    & $csc /nologo /target:exe /platform:anycpu /optimize+ "/out:$saveStorageTest" (Join-Path $releaseRoot 'tests\GameSaveStorageSmoke.cs') (Join-Path $releaseRoot 'src\GameSaveStorage.cs')
    if ($LASTEXITCODE) { throw "Saved-pilot preservation test compilation failed with exit code $LASTEXITCODE." }
    & $saveStorageTest
    if ($LASTEXITCODE) { throw "Saved-pilot preservation tests failed with exit code $LASTEXITCODE." }
    $shortcutPolicyTest = Join-Path $smoke 'LauncherShortcutPolicySmoke.exe'
    & $csc /nologo /target:exe /platform:anycpu /optimize+ "/out:$shortcutPolicyTest" /reference:Microsoft.CSharp.dll (Join-Path $releaseRoot 'tests\LauncherShortcutPolicySmoke.cs') (Join-Path $releaseRoot 'src\LauncherShortcutPolicy.cs')
    if ($LASTEXITCODE) { throw "Launcher shortcut policy test compilation failed with exit code $LASTEXITCODE." }
    & $shortcutPolicyTest
    if ($LASTEXITCODE) { throw "Launcher shortcut policy tests failed with exit code $LASTEXITCODE." }
    $launcherRecoveryTest = Join-Path $smoke 'LauncherRecoverySmoke.exe'
    & $csc /nologo /target:exe /platform:anycpu /optimize+ "/out:$launcherRecoveryTest" (Join-Path $releaseRoot 'tests\LauncherRecoverySmoke.cs') (Join-Path $releaseRoot 'src\LauncherRecovery.cs') (Join-Path $releaseRoot 'src\InstalledProcessScope.cs')
    if ($LASTEXITCODE) { throw "Launcher recovery test compilation failed with exit code $LASTEXITCODE." }
    & $launcherRecoveryTest
    if ($LASTEXITCODE) { throw "Launcher recovery tests failed with exit code $LASTEXITCODE." }
    $audioParentLifetimeTest = Join-Path $smoke 'CdAudioParentLifetimeSmoke.exe'
    & $csc /nologo /target:exe /platform:anycpu /optimize+ "/out:$audioParentLifetimeTest" (Join-Path $releaseRoot 'tests\CdAudioParentLifetimeSmoke.cs')
    if ($LASTEXITCODE) { throw "CD audio parent-lifetime test compilation failed with exit code $LASTEXITCODE." }
    $type.GetMethod('ExtractPayload', $flags).Invoke($null, [object[]]@($payload))
    $uninstallerAssembly = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $payload 'Uninstall.exe')))
    $uninstallerType = $uninstallerAssembly.GetType('Uninstaller', $true)
    $cleanupWorkerPath = [string](Join-Path ([IO.Path]::GetTempPath()) 'worker.exe')
    $cleanupInstallPath = [string](Join-Path $smoke 'installed')
    $cleanupInfo = $uninstallerType.GetMethod('CreateCleanupStartInfo', $flags).Invoke($null,
        [object[]]@($cleanupWorkerPath, $cleanupInstallPath, [int]1234))
    if ($cleanupInfo.UseShellExecute -or
        -not ([IO.Path]::GetFullPath($cleanupInfo.WorkingDirectory).Equals([IO.Path]::GetFullPath([IO.Path]::GetTempPath()), [StringComparison]::OrdinalIgnoreCase))) {
        throw 'Uninstaller cleanup worker does not run outside the installation directory.'
    }
    Write-Host 'Uninstaller cleanup working-directory contract passed.'
    # Load from bytes so the smoke harness does not retain a file lock that
    # prevents its disposable payload tree from being removed.
    $launcherAssembly = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $payload 'MW3Launcher.exe')))
    $launcherType = $launcherAssembly.GetType('LauncherForm', $true)
    $launcherForm = [Activator]::CreateInstance($launcherType, $true)
    try {
        $uninstallButtons = @($launcherForm.Controls | Where-Object { $_ -is [System.Windows.Forms.Button] -and $_.Text -eq 'UNINSTALL' })
        if ($uninstallButtons.Count -ne 1) { throw 'The launcher must expose exactly one UNINSTALL button.' }
        if ($uninstallButtons[0].Right -ne 720 -or $uninstallButtons[0].Bottom -ne 432) { throw 'The launcher UNINSTALL button is not in the expected bottom-right position.' }
        $controlsButtons = @($launcherForm.Controls | Where-Object { $_ -is [System.Windows.Forms.Button] -and $_.Text -eq 'CONTROLS' })
        if ($controlsButtons.Count -ne 1 -or $controlsButtons[0].Right -ge $uninstallButtons[0].Left) {
            throw 'The launcher controls action is missing or overlaps uninstall.'
        }
        Write-Host 'Launcher controls and uninstall actions: passed.'
    }
    finally { $launcherForm.Dispose() }
    # Isolate the helper from the payload's winmm proxy. The installed helper
    # lives under mcicda and does not load the root-level proxy beside itself.
    $audioParentFixture = Join-Path $smoke 'audio-parent-fixture'
    New-Item -ItemType Directory -Path $audioParentFixture | Out-Null
    $audioParentPlayer = Join-Path $audioParentFixture 'cdaudioplr.exe'
    Copy-Item -LiteralPath (Join-Path $payload 'compat\cdaudioplr.exe') -Destination $audioParentPlayer
    Copy-Item -LiteralPath (Join-Path $payload 'compat\NLayer.dll') -Destination (Join-Path $audioParentFixture 'NLayer.dll')
    & $audioParentLifetimeTest $audioParentPlayer
    if ($LASTEXITCODE) { throw "CD audio parent-lifetime test failed with exit code $LASTEXITCODE." }
    $extractor = [string](Join-Path $payload 'tools\UnshieldSharp.exe')
    $qualifiedDdrawHash = 'FD11B9B6B8A8CC23744DFEDC10E23798860F3A96BD8B2B4C75DD2FDB3BE8F8FB'
    if ($Mw3IsoPath) {
        Invoke-WithMountedIso $assembly $Mw3IsoPath {
            param($discRoot)
            $mediaType = $assembly.GetType('DiscMediaSession', $true)
            $logger = [Action[string]]{ param($message) Write-Host $message }
            $folderMedia = $mediaType.GetMethod('Open').Invoke($null, [object[]]@($discRoot, $logger))
            try {
                if ($folderMedia.OwnsMount -or $folderMedia.Root -ne $discRoot) {
                    throw 'The externally mounted disc folder was not preserved as a borrowed media root.'
                }
                Install-DiscGameSmoke $type $flags $folderMedia.Root $game $extractor $payload $false
            }
            finally { $folderMedia.Dispose() }
            if (-not (Test-Path -LiteralPath $discRoot -PathType Container)) {
                throw 'Disposing borrowed disc media unexpectedly ejected its mount.'
            }
        }
        $mw3MediaResult = 'passed (mounted ISO and borrowed folder, extraction, video copy, official patch)'
        Assert-InstalledGameSmoke $game $false $qualifiedDdrawHash -RequireDiscVideo
    }
    else {
        $disc = [string](Resolve-Path (Join-Path $releaseRoot '..\staging\installshield')).Path
        Install-DiscGameSmoke $type $flags $disc $game $extractor $payload $false
        $mw3MediaResult = 'passed (expanded InstallShield fixture, extraction, official patch)'
        Assert-InstalledGameSmoke $game $false $qualifiedDdrawHash
    }

    $audioOutputResult = 'not requested'
    if ($VerifyAudioOutput) {
        $audioOutputProbe = Join-Path $smoke 'AudioOutputProbe.exe'
        & $csc /nologo /target:exe /platform:x86 /optimize+ "/out:$audioOutputProbe" "/reference:$(Join-Path $game 'mcicda\NLayer.dll')" "/reference:$netStandard" (Join-Path $releaseRoot 'tests\AudioOutputProbe.cs') (Join-Path $releaseRoot 'src\Mp3WaveOutPlayer.cs')
        if ($LASTEXITCODE) { throw "Audio-output probe compilation failed with exit code $LASTEXITCODE." }
        Copy-Item -LiteralPath (Join-Path $game 'mcicda\NLayer.dll') -Destination (Join-Path $smoke 'NLayer.dll')
        & $audioOutputProbe (Join-Path $game 'mcicda\music\track02.mp3')
        if ($LASTEXITCODE) { throw "MechWarrior 3 audio-output playback probe failed with exit code $LASTEXITCODE." }
        $audioOutputResult = 'passed for MechWarrior 3'
    }

    $audioTestsEnabled = -not [bool](Get-Process -Name cdaudioplr -ErrorAction SilentlyContinue)
    $cdAudioProtocol = 'skipped (another CD audio player is running)'
    if ($audioTestsEnabled) {
        $audioLifecycleTest = Join-Path $smoke 'InstalledProcessScopeSmoke.exe'
        & $csc /nologo /target:exe /platform:anycpu /optimize+ "/out:$audioLifecycleTest" (Join-Path $releaseRoot 'tests\InstalledProcessScopeSmoke.cs') (Join-Path $releaseRoot 'src\InstalledProcessScope.cs')
        if ($LASTEXITCODE) { throw "CD audio lifecycle test compilation failed with exit code $LASTEXITCODE." }
        $protocolTest = Join-Path $smoke 'CdAudioProtocolSmoke.exe'
        & $csc /nologo /target:exe /platform:anycpu /optimize+ "/out:$protocolTest" (Join-Path $releaseRoot 'tests\CdAudioProtocolSmoke.cs')
        if ($LASTEXITCODE) { throw "CD audio protocol test compilation failed with exit code $LASTEXITCODE." }
        Invoke-InstalledAudioSmoke $audioLifecycleTest $protocolTest $game 3 'MechWarrior 3' -VerifyPlayback:$VerifyAudioOutput
        $cdAudioProtocol = 'passed for MechWarrior 3 (tracks 2-3, shutdown)'
    }

    $piratesMoonIsoResult = 'not requested'
    if ($PiratesMoonIsoPath) {
        $pmIsoGame = [string](Join-Path $smoke 'pirates-moon-iso-game')
        Invoke-WithMountedIso $assembly $PiratesMoonIsoPath {
            param($discRoot)
            Install-DiscGameSmoke $type $flags $discRoot $pmIsoGame $extractor $payload $true
        }
        Assert-InstalledGameSmoke $pmIsoGame $true $qualifiedDdrawHash
        if ($audioTestsEnabled) {
            Invoke-InstalledAudioSmoke $audioLifecycleTest $protocolTest $pmIsoGame 4 "Pirate's Moon ISO" -VerifyPlayback:$VerifyAudioOutput
            $cdAudioProtocol += "; Pirate's Moon ISO (tracks 2-4, shutdown)"
        }
        if ($VerifyAudioOutput) {
            & $audioOutputProbe (Join-Path $pmIsoGame 'mcicda\music\track02.mp3')
            if ($LASTEXITCODE) { throw "Pirate's Moon ISO audio-output playback probe failed with exit code $LASTEXITCODE." }
            $audioOutputResult += "; Pirate's Moon ISO"
        }
        $piratesMoonIsoResult = 'passed (mounted ISO, extraction, retail-to-no-disc patch, shared installed-game contract)'
    }

    $piratesMoonArchiveResult = 'not requested'
    if ($PiratesMoonArchivePath) {
        $expandedArchive = [string](Join-Path $smoke 'pirates-moon-archive')
        $convertedIso = [string](Join-Path $smoke 'pirates-moon-archive.iso')
        Invoke-InstallerMethod $type $flags 'ExtractZipSafely' ([object[]]@([string](Resolve-Path $PiratesMoonArchivePath), $expandedArchive))
        $pmMediaType = $assembly.GetType('PiratesMoonMedia', $true)
        $pmMediaType.GetMethod('CreateMountableIsoFromExtractedArchive', $flags).Invoke($null, [object[]]@($expandedArchive, $convertedIso))
        $pmArchiveGame = [string](Join-Path $smoke 'pirates-moon-archive-game')
        Invoke-WithMountedIso $assembly $convertedIso {
            param($discRoot)
            Install-DiscGameSmoke $type $flags $discRoot $pmArchiveGame $extractor $payload $true
        }
        Assert-InstalledGameSmoke $pmArchiveGame $true $qualifiedDdrawHash
        if ($audioTestsEnabled) {
            Invoke-InstalledAudioSmoke $audioLifecycleTest $protocolTest $pmArchiveGame 4 "Pirate's Moon ISO-version ZIP" -VerifyPlayback:$VerifyAudioOutput
            $cdAudioProtocol += "; Pirate's Moon ISO-version ZIP (tracks 2-4, shutdown)"
        }
        if ($VerifyAudioOutput) {
            & $audioOutputProbe (Join-Path $pmArchiveGame 'mcicda\music\track02.mp3')
            if ($LASTEXITCODE) { throw "Pirate's Moon ISO-version ZIP audio-output playback probe failed with exit code $LASTEXITCODE." }
            $audioOutputResult += "; Pirate's Moon ISO-version ZIP"
        }
        $piratesMoonArchiveResult = 'passed (safe ZIP extraction, Mode-1 BIN/CUE conversion, mount, game extraction, retail-to-no-disc patch, shared installed-game contract)'
    }

    $piratesMoonRipResult = 'not requested'
    if ($VerifyPiratesMoonRip) {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $ripSource = [string](Resolve-Path (Join-Path $releaseRoot "..\Installation Files\Mechwarrior 3 Pirates Moon - RIP")).Path
        $pmMediaTest = Join-Path $smoke 'PiratesMoonMediaSmoke.exe'
        & $csc /nologo /target:exe /platform:anycpu /optimize+ "/out:$pmMediaTest" (Join-Path $releaseRoot 'tests\PiratesMoonMediaSmoke.cs') (Join-Path $releaseRoot 'src\PiratesMoonMedia.cs')
        if ($LASTEXITCODE) { throw "Pirate's Moon media policy test compilation failed with exit code $LASTEXITCODE." }
        & $pmMediaTest (Join-Path $ripSource 'mech3.exe')
        if ($LASTEXITCODE) { throw "Pirate's Moon media policy tests failed with exit code $LASTEXITCODE." }
        $ripArchive = [string](Join-Path $smoke 'pirates-moon-rip.zip')
        [IO.Compression.ZipFile]::CreateFromDirectory($ripSource, $ripArchive, [IO.Compression.CompressionLevel]::Fastest, $true)
        $expandedRip = [string](Join-Path $smoke 'expanded-rip')
        $pmGame = [string](Join-Path $smoke 'pirates-moon-game')
        Invoke-InstallerMethod $type $flags 'ExtractZipSafely' ([object[]]@($ripArchive, $expandedRip))
        $ripRoot = [string](Invoke-InstallerMethod $type $flags 'FindPiratesMoonRipRoot' ([object[]]@($expandedRip)))
        Invoke-InstallerMethod $type $flags 'ExtractPiratesMoonRip' ([object[]]@($ripRoot, $pmGame))
        Invoke-InstallerMethod $type $flags 'InstallCompatibility' ([object[]]@($payload, $pmGame, [bool]$true))
        Assert-InstalledGameSmoke $pmGame $true $qualifiedDdrawHash
        if ($audioTestsEnabled) {
            Invoke-InstalledAudioSmoke $audioLifecycleTest $protocolTest $pmGame 4 "Pirate's Moon RIP" -VerifyPlayback:$VerifyAudioOutput
            $cdAudioProtocol += "; Pirate's Moon RIP (tracks 2-4, shutdown)"
        }
        if ($VerifyAudioOutput) {
            & $audioOutputProbe (Join-Path $pmGame 'mcicda\music\track02.mp3')
            if ($LASTEXITCODE) { throw "Pirate's Moon RIP audio-output playback probe failed with exit code $LASTEXITCODE." }
            $audioOutputResult += "; Pirate's Moon RIP"
        }
        $piratesMoonRipResult = 'passed (ZIP, filtered RIP, shared installed-game contract)'
    }
    $forbidden = Get-ChildItem -LiteralPath $payload -Recurse -Force -File | Where-Object {
        $_.Extension -ieq '.iso' -or $_.Name -ieq '.env' -or $_.Name -like '.env.*'
    }
    if ($forbidden) { throw "Forbidden payload files found: $($forbidden.FullName -join ', ')" }

    [PSCustomObject]@{
        SetupSHA256 = (Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash
        SetupMB = [math]::Round((Get-Item -LiteralPath $setup).Length / 1MB, 2)
        PayloadFiles = (Get-ChildItem -LiteralPath $payload -Recurse -File).Count
        PatchedExeSHA256 = (Get-FileHash -LiteralPath (Join-Path $game 'Mech3fixup.exe') -Algorithm SHA256).Hash
        ForbiddenFiles = 0
        RendererProfile = 'field baseline r18 with primary-surface recovery and Pirate''s Moon-only profile'
        CdAudioProtocol = $cdAudioProtocol
        AudioOutput = $audioOutputResult
        Mw3Media = $mw3MediaResult
        PiratesMoonIso = $piratesMoonIsoResult
        PiratesMoonArchive = $piratesMoonArchiveResult
        PiratesMoonRip = $piratesMoonRipResult
    } | Format-List
}
finally {
    if (-not $KeepSmokeTree -and (Test-Path -LiteralPath $smoke)) {
        $resolved = (Resolve-Path -LiteralPath $smoke).Path
        if ($resolved.StartsWith($releaseRoot, [StringComparison]::OrdinalIgnoreCase)) {
            Remove-Item -LiteralPath $resolved -Recurse -Force
        }
    }
}
