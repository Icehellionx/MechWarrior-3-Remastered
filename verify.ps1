[CmdletBinding()]
param(
    [switch]$KeepSmokeTree,
    [switch]$VerifyPiratesMoonRip
)

$ErrorActionPreference = 'Stop'
$releaseRoot = $PSScriptRoot
$smoke = Join-Path $releaseRoot 'smoke-test'
if (Test-Path -LiteralPath $smoke) {
    $resolved = (Resolve-Path -LiteralPath $smoke).Path
    if (-not $resolved.StartsWith($releaseRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe smoke-test path.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
New-Item -ItemType Directory -Path $smoke | Out-Null

try {
    $setup = (Resolve-Path (Join-Path $releaseRoot 'dist\MechWarrior-3-Remastered-Setup.exe')).Path
    $assembly = [Reflection.Assembly]::LoadFile($setup)
    $type = $assembly.GetType('InstallerForm')
    $flags = [Reflection.BindingFlags]'NonPublic,Static'
    $payload = [string](Join-Path $smoke 'payload')
    $game = [string](Join-Path $smoke 'game')
    New-Item -ItemType Directory -Path $payload | Out-Null
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    $registryTest = Join-Path $smoke 'GameInstallRegistrySmoke.exe'
    & $csc /nologo /target:exe /platform:anycpu /optimize+ "/out:$registryTest" (Join-Path $releaseRoot 'tests\GameInstallRegistrySmoke.cs') (Join-Path $releaseRoot 'src\GameInstallRegistry.cs')
    if ($LASTEXITCODE) { throw "Game install registry test compilation failed with exit code $LASTEXITCODE." }
    & $registryTest
    if ($LASTEXITCODE) { throw "Game install registry tests failed with exit code $LASTEXITCODE." }
    $launcherRecoveryTest = Join-Path $smoke 'LauncherRecoverySmoke.exe'
    & $csc /nologo /target:exe /platform:anycpu /optimize+ "/out:$launcherRecoveryTest" (Join-Path $releaseRoot 'tests\LauncherRecoverySmoke.cs') (Join-Path $releaseRoot 'src\LauncherRecovery.cs') (Join-Path $releaseRoot 'src\InstalledProcessScope.cs')
    if ($LASTEXITCODE) { throw "Launcher recovery test compilation failed with exit code $LASTEXITCODE." }
    & $launcherRecoveryTest
    if ($LASTEXITCODE) { throw "Launcher recovery tests failed with exit code $LASTEXITCODE." }
    $audioParentLifetimeTest = Join-Path $smoke 'CdAudioParentLifetimeSmoke.exe'
    & $csc /nologo /target:exe /platform:anycpu /optimize+ "/out:$audioParentLifetimeTest" (Join-Path $releaseRoot 'tests\CdAudioParentLifetimeSmoke.cs')
    if ($LASTEXITCODE) { throw "CD audio parent-lifetime test compilation failed with exit code $LASTEXITCODE." }
    $type.GetMethod('ExtractPayload', $flags).Invoke($null, [object[]]@($payload))
    # Isolate the helper from the payload's winmm proxy. The installed helper
    # lives under mcicda and does not load the root-level proxy beside itself.
    $audioParentFixture = Join-Path $smoke 'audio-parent-fixture'
    New-Item -ItemType Directory -Path $audioParentFixture | Out-Null
    $audioParentPlayer = Join-Path $audioParentFixture 'cdaudioplr.exe'
    Copy-Item -LiteralPath (Join-Path $payload 'compat\cdaudioplr.exe') -Destination $audioParentPlayer
    & $audioParentLifetimeTest $audioParentPlayer
    if ($LASTEXITCODE) { throw "CD audio parent-lifetime test failed with exit code $LASTEXITCODE." }
    $disc = [string](Resolve-Path (Join-Path $releaseRoot '..\staging\installshield')).Path
    $extractor = [string](Join-Path $payload 'tools\UnshieldSharp.exe')
    $type.GetMethod('ExtractGame', $flags).Invoke($null, [object[]]@($disc, $game, $extractor))
    $patch = [string](Join-Path $payload 'patch12')
    $type.GetMethod('ApplyPatch12', $flags).Invoke($null, [object[]]@($patch, $game))
    $type.GetMethod('InstallCompatibility', $flags).Invoke($null, [object[]]@($payload, $game, [bool]$false))

    $required = @(
        'Mech3.exe', 'Mech3fixup.exe', 'ddraw.dll', 'zipfixup.dll', 'winmm.dll',
        'mcicda\cdaudioplr.exe',
        'mcicda\music\track02.mp3',
        'common-shaders-master\mw3-remaster\mw3-remaster.cg'
    )
    foreach ($relative in $required) {
        $path = Join-Path $game $relative
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Smoke test output is missing: $relative" }
    }

    $rendererConfig = Get-Content -LiteralPath (Join-Path $game 'DDrawCompat.ini') -Raw
    # Field baseline contract: do not remove these individually during a renderer rollback.
    $requiredRendererSettings = @(
        'FullscreenMode = borderless',
        'AltTabFix = keepvidmem(1)',
        'DisplayAspectRatio = 4:3',
        'RemasterIntroWidescreen = on',
        'RemasterIntroChromaCleanup = on',
        'PresentationEdgeRepair = 4'
        'Antialiasing = msaa4x(0)'
    )
    foreach ($setting in $requiredRendererSettings) {
        if (-not $rendererConfig.Contains($setting)) { throw "Release renderer profile is missing: $setting" }
    }
    if ($rendererConfig.Contains('RemasterTexture') -or $rendererConfig.Contains('RemasterTelemetry')) {
        throw 'Release renderer profile contains texture or telemetry hooks.'
    }
    $qualifiedDdrawHash = 'FD11B9B6B8A8CC23744DFEDC10E23798860F3A96BD8B2B4C75DD2FDB3BE8F8FB'
    $actualDdrawHash = (Get-FileHash -LiteralPath (Join-Path $game 'ddraw.dll') -Algorithm SHA256).Hash
    if ($actualDdrawHash -ne $qualifiedDdrawHash) {
        throw "Release contains an unqualified ddraw.dll: $actualDdrawHash"
    }

    $cdAudioProtocol = 'skipped (another CD audio player is running)'
    if (-not (Get-Process -Name cdaudioplr -ErrorAction SilentlyContinue)) {
        $audioLifecycleTest = Join-Path $smoke 'InstalledProcessScopeSmoke.exe'
        & $csc /nologo /target:exe /platform:anycpu /optimize+ "/out:$audioLifecycleTest" (Join-Path $releaseRoot 'tests\InstalledProcessScopeSmoke.cs') (Join-Path $releaseRoot 'src\InstalledProcessScope.cs')
        if ($LASTEXITCODE) { throw "CD audio lifecycle test compilation failed with exit code $LASTEXITCODE." }
        & $audioLifecycleTest $game
        if ($LASTEXITCODE) { throw "CD audio lifecycle test failed with exit code $LASTEXITCODE." }

        $protocolTest = Join-Path $smoke 'CdAudioProtocolSmoke.exe'
        & $csc /nologo /target:exe /platform:anycpu /optimize+ "/out:$protocolTest" (Join-Path $releaseRoot 'tests\CdAudioProtocolSmoke.cs')
        if ($LASTEXITCODE) { throw "CD audio protocol test compilation failed with exit code $LASTEXITCODE." }
        & $protocolTest (Join-Path $game 'mcicda\cdaudioplr.exe') 3
        if ($LASTEXITCODE) { throw "CD audio protocol smoke test failed with exit code $LASTEXITCODE." }
        $cdAudioProtocol = 'passed (3 tracks, shutdown)'
    }

    $piratesMoonResult = 'not requested'
    if ($VerifyPiratesMoonRip) {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $ripSource = [string](Resolve-Path (Join-Path $releaseRoot "..\Installation Files\Mechwarrior 3 Pirates Moon - RIP")).Path
        $ripArchive = [string](Join-Path $smoke 'pirates-moon-rip.zip')
        [IO.Compression.ZipFile]::CreateFromDirectory($ripSource, $ripArchive, [IO.Compression.CompressionLevel]::Fastest, $true)
        $expandedRip = [string](Join-Path $smoke 'expanded-rip')
        $pmGame = [string](Join-Path $smoke 'pirates-moon-game')
        $type.GetMethod('ExtractZipSafely', $flags).Invoke($null, [object[]]@($ripArchive, $expandedRip))
        $ripRoot = [string]$type.GetMethod('FindPiratesMoonRipRoot', $flags).Invoke($null, [object[]]@($expandedRip))
        $type.GetMethod('ExtractPiratesMoonRip', $flags).Invoke($null, [object[]]@($ripRoot, $pmGame))
        $type.GetMethod('InstallCompatibility', $flags).Invoke($null, [object[]]@($payload, $pmGame, [bool]$true))
        if (-not (Test-Path -LiteralPath (Join-Path $pmGame 'Mech3fixup.exe') -PathType Leaf)) { throw "Pirate's Moon ZIP smoke test did not produce Mech3fixup.exe." }
        $pmDdrawConfig = Get-Content -LiteralPath (Join-Path $pmGame 'DDrawCompat.ini') -Raw
        if ($pmDdrawConfig -notmatch '(?m)^LogLevel\s*=\s*info\s*$') { throw "Pirate's Moon compatibility config did not use bounded release logging." }
        if ($pmDdrawConfig -notmatch '(?m)^VSync\s*=\s*on\s*$') { throw "Pirate's Moon compatibility config did not enable stable VSync presentation." }
        if ($pmDdrawConfig -notmatch '(?m)^PresentDelay\s*=\s*on\(50\)\s*$') { throw "Pirate's Moon compatibility config did not enable partial-frame coalescing." }
        foreach ($baseOnlySetting in @('CpuAffinityRotation', 'RemasterIntroWidescreen', 'RemasterIntroChromaCleanup', 'PresentationEdgeRepair')) {
            if ($pmDdrawConfig -match "(?m)^$baseOnlySetting\s*=") { throw "Pirate's Moon compatibility config unexpectedly contains base-game-only setting: $baseOnlySetting" }
        }
        if ($pmDdrawConfig -match '(?m)^RemasterStartupSurfaceClear\s*=') { throw "Pirate's Moon compatibility config retained the disproven startup surface clear." }
        if (Test-Path -LiteralPath (Join-Path $pmGame 'CRACK')) { throw "Pirate's Moon ZIP smoke test copied the CRACK directory." }
        if (Test-Path -LiteralPath (Join-Path $pmGame 'CLASS.NFO.txt')) { throw "Pirate's Moon ZIP smoke test copied the NFO." }
        $piratesMoonResult = 'passed (ZIP, filtered RIP, no-disc executable, ZipperFixup, VSync, 50 ms presentation delay)'
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
        PiratesMoonRip = $piratesMoonResult
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
