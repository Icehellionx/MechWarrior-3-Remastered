[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][string]$GameRoot,
    [string]$CandidateRoot,
    [switch]$Restore
)

$ErrorActionPreference = 'Stop'
$game = (Resolve-Path -LiteralPath $GameRoot).Path
$zbd = Join-Path $game 'zbd'
if (-not (Test-Path -LiteralPath $zbd -PathType Container)) { throw 'The selected game has no zbd directory.' }
if ((Get-Item -LiteralPath $game).Attributes -band [IO.FileAttributes]::ReparsePoint -or
    (Get-Item -LiteralPath $zbd).Attributes -band [IO.FileAttributes]::ReparsePoint) {
    throw 'Linked game or zbd directories are not accepted.'
}
if (Get-Process -Name Mech3fixup,Mech3 -ErrorAction SilentlyContinue) {
    throw 'Close MechWarrior 3 before changing its sound archives.'
}

$original = @{
    'soundsH.zbd' = '71C4688E38D59E03D3E0A63C8EF90CCE0359103890904AAAB5DC461647F484A4'
    'soundsL.zbd' = 'E259704B36069339BAD035AE571F7733A6655ED2020C4D7E24B5C8872A3B09C3'
}
$leveled = @{
    'soundsH.zbd' = '20AD72D51EAFFB4447A7CE09B408B017CFAA5A7034A82E73B85B539355579BCB'
    'soundsL.zbd' = '612F8EDB1E26884AAD04E34F3E73D41D7661F8652F97EB765C8C28EAC9A37D98'
}
$names = @('soundsH.zbd', 'soundsL.zbd')
$backup = Join-Path $game 'OriginalSoundArchives'
if (Test-Path -LiteralPath $backup -PathType Container) {
    if ((Get-Item -LiteralPath $backup).Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw 'A linked backup directory is not accepted.'
    }
}
if (-not $Restore) {
    if (-not $CandidateRoot) { throw 'Provide CandidateRoot when applying the sound-level candidate.' }
    $candidate = (Resolve-Path -LiteralPath $CandidateRoot).Path
    if ((Get-Item -LiteralPath $candidate).Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw 'A linked candidate directory is not accepted.'
    }
}

function Assert-Hash([string]$Path, [string]$Expected) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Missing sound archive: $Path" }
    if ((Get-Item -LiteralPath $Path).Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw "Linked sound archives are not accepted: $Path"
    }
    $actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
    if ($actual -ne $Expected) { throw "Sound archive hash mismatch: $Path" }
}

# Validate both sides before any replacement.
foreach ($name in $names) {
    $installed = Join-Path $zbd $name
    if ((Get-Item -LiteralPath $installed).Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw "Linked installed sound archives are not accepted: $installed"
    }
    if ($Restore) {
        Assert-Hash (Join-Path $backup $name) $original[$name]
        $hash = (Get-FileHash -LiteralPath $installed -Algorithm SHA256).Hash
        if ($hash -ne $original[$name] -and $hash -ne $leveled[$name]) {
            throw "Installed sound archive hash mismatch: $installed"
        }
    }
    else {
        Assert-Hash (Join-Path $candidate $name) $leveled[$name]
        Assert-Hash $installed $original[$name]
        if (Test-Path -LiteralPath (Join-Path $backup $name)) {
            Assert-Hash (Join-Path $backup $name) $original[$name]
        }
    }
}

if (-not $PSCmdlet.ShouldProcess($game, $(if ($Restore) { 'Restore original game sound archives' } else { 'Apply local sound-level candidate' }))) { return }

if (-not $Restore) {
    New-Item -ItemType Directory -Path $backup -Force | Out-Null
    if ((Get-Item -LiteralPath $backup).Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw 'A linked backup directory is not accepted.'
    }
    foreach ($name in $names) {
        $saved = Join-Path $backup $name
        if (-not (Test-Path -LiteralPath $saved)) {
            [IO.File]::Copy((Join-Path $zbd $name), $saved)
            Assert-Hash $saved $original[$name]
        }
    }
}

try {
    foreach ($name in $names) {
        $target = Join-Path $zbd $name
        $source = if ($Restore) { Join-Path $backup $name } else { Join-Path $candidate $name }
        $expected = if ($Restore) { $original[$name] } else { $leveled[$name] }
        $temporary = $target + '.leveling.tmp'
        $replacementBackup = $target + '.leveling.replace-backup'
        if ((Test-Path -LiteralPath $temporary) -or (Test-Path -LiteralPath $replacementBackup)) {
            throw "A temporary sound archive already exists beside $target"
        }
        $attributes = [IO.File]::GetAttributes($target)
        try {
            [IO.File]::Copy($source, $temporary)
            Assert-Hash $temporary $expected
            [IO.File]::SetAttributes($target, [IO.FileAttributes]::Normal)
            [IO.File]::Replace($temporary, $target, $replacementBackup)
            [IO.File]::SetAttributes($target, $attributes)
            Assert-Hash $target $expected
            [IO.File]::Delete($replacementBackup)
        }
        finally { if (Test-Path -LiteralPath $temporary) { [IO.File]::Delete($temporary) } }
    }
}
catch {
    foreach ($name in $names) {
        $target = Join-Path $zbd $name
        $saved = Join-Path $backup $name
        if (Test-Path -LiteralPath $saved) {
            try {
                [IO.File]::SetAttributes($target, [IO.FileAttributes]::Normal)
                [IO.File]::Copy($saved, $target, $true)
                Assert-Hash $target $original[$name]
            }
            catch { Write-Warning "Could not restore $name automatically; the original remains in OriginalSoundArchives." }
        }
    }
    throw
}

Write-Host $(if ($Restore) { 'Original sound archives restored.' } else { 'Sound-level candidate applied; originals saved in OriginalSoundArchives.' })
