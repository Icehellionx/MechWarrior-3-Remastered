[CmdletBinding()]
param([switch]$KeepSmokeTree)

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
    $type.GetMethod('ExtractPayload', $flags).Invoke($null, [object[]]@($payload))
    $disc = [string](Resolve-Path (Join-Path $releaseRoot '..\staging\installshield')).Path
    $extractor = [string](Join-Path $payload 'tools\UnshieldSharp.exe')
    $type.GetMethod('ExtractGame', $flags).Invoke($null, [object[]]@($disc, $game, $extractor))
    $patch = [string](Join-Path $payload 'patch12')
    $type.GetMethod('ApplyPatch12', $flags).Invoke($null, [object[]]@($patch, $game))
    $type.GetMethod('InstallCompatibility', $flags).Invoke($null, [object[]]@($payload, $game, [bool]$false))

    $required = @(
        'Mech3.exe', 'Mech3fixup.exe', 'ddraw.dll', 'zipfixup.dll', 'winmm.dll',
        'mcicda\music\track02.mp3',
        'common-shaders-master\mw3-remaster\mw3-remaster.cg'
    )
    foreach ($relative in $required) {
        $path = Join-Path $game $relative
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Smoke test output is missing: $relative" }
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
