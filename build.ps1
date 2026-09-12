[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$releaseRoot = $PSScriptRoot
$projectRoot = Split-Path -Parent $releaseRoot
$objRoot = Join-Path $releaseRoot 'obj'
$payloadRoot = Join-Path $objRoot 'payload'
$distRoot = Join-Path $releaseRoot 'dist'

if (Test-Path -LiteralPath $objRoot) { Remove-Item -LiteralPath $objRoot -Recurse -Force }
New-Item -ItemType Directory -Path $payloadRoot, $distRoot -Force | Out-Null

function Copy-ReleaseFile {
    param([Parameter(Mandatory)][string]$Source, [Parameter(Mandatory)][string]$Destination)
    if (-not (Test-Path -LiteralPath $Source -PathType Leaf)) { throw "Missing build input: $Source" }
    $parent = Split-Path -Parent $Destination
    if ($parent) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    Copy-Item -LiteralPath $Source -Destination $Destination -Force
}

# Build the small runtime programs first so they become part of the one-file setup payload.
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $csc -PathType Leaf)) { throw 'The Windows .NET Framework C# compiler is unavailable.' }
$cdAudioPlayer = Join-Path $payloadRoot 'compat\cdaudioplr.exe'
New-Item -ItemType Directory -Path (Split-Path -Parent $cdAudioPlayer) -Force | Out-Null
& $csc /nologo /target:winexe /platform:x86 /optimize+ "/out:$cdAudioPlayer" "$releaseRoot\src\CdAudioPlayer.cs"
if ($LASTEXITCODE) { throw "CD audio player compilation failed with exit code $LASTEXITCODE." }
$launcher = Join-Path $payloadRoot 'MW3Launcher.exe'
& $csc /nologo /target:winexe /platform:anycpu /optimize+ "/win32manifest:$releaseRoot\src\launcher.manifest" "/win32icon:$releaseRoot\assets\MW3-Remastered.ico" "/resource:$releaseRoot\assets\MW3-Game.png,MW3.Game.png" "/resource:$releaseRoot\assets\Pirates-Moon-Game.png,PiratesMoon.Game.png" "/resource:$releaseRoot\assets\MW3-Manual-Cover.png,MW3.ManualCover.png" "/resource:$releaseRoot\assets\Pirates-Moon-Manual-Cover.png,PiratesMoon.ManualCover.png" "/out:$launcher" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "$releaseRoot\src\Launcher.cs" "$releaseRoot\src\LauncherRecovery.cs" "$releaseRoot\src\InstalledProcessScope.cs" "$releaseRoot\src\GameInstallRegistry.cs"
if ($LASTEXITCODE) { throw "Launcher compilation failed with exit code $LASTEXITCODE." }
$uninstaller = Join-Path $payloadRoot 'Uninstall.exe'
& $csc /nologo /target:winexe /platform:x64 /optimize+ "/win32manifest:$releaseRoot\src\app.manifest" "/win32icon:$releaseRoot\assets\MW3-Remastered.ico" "/out:$uninstaller" /reference:System.Windows.Forms.dll "$releaseRoot\src\Uninstaller.cs" "$releaseRoot\src\InstalledProcessScope.cs" "$releaseRoot\src\GameInstallRegistry.cs"
if ($LASTEXITCODE) { throw "Uninstaller compilation failed with exit code $LASTEXITCODE." }

# Extraction tool: used temporarily and not left in the installed game.
Copy-ReleaseFile "$projectRoot\tools\UnshieldSharp\UnshieldSharp.exe" "$payloadRoot\tools\UnshieldSharp.exe"

# Official patch payload only; no base-game or expansion content is copied here.
Copy-Item -LiteralPath "$projectRoot\staging\patch12-payload" -Destination "$payloadRoot\patch12" -Recurse

# Current compatibility/remaster binaries.
Copy-ReleaseFile "$projectRoot\tools\ZipperFixup\target\i686-pc-windows-msvc\release\zipfixup.dll" "$payloadRoot\compat\zipfixup.dll"
Copy-ReleaseFile "$projectRoot\tools\ZipperFixup\target\i686-pc-windows-msvc\release\zippatch.exe" "$payloadRoot\compat\zfapply.exe"
# Ship the clean v0.7.1-derived wrapper with isolated presentation fixes and
# the separately switchable INTRO.AVI dark-chroma cleanup experiment.
# The development tree contains broader experimental hooks and is not a release input.
Copy-ReleaseFile "$projectRoot\tools\releases\DDrawCompat-MW3-field-baseline-v0.7.1-r18-primary-surface-retry\ddraw.dll" "$payloadRoot\compat\ddraw.dll"
Copy-ReleaseFile "$projectRoot\tools\releases\cdaudio-winmm-0.4.0.3\package\winmm.dll" "$payloadRoot\compat\winmm.dll"
Copy-ReleaseFile "$projectRoot\tools\releases\cdaudio-winmm-0.4.0.3\package\mcicda\cdaudio_vol.ini" "$payloadRoot\compat\cdaudio_vol.ini"

Copy-ReleaseFile "$releaseRoot\config\DDrawCompat.ini" "$payloadRoot\config\DDrawCompat.ini"
Copy-ReleaseFile "$releaseRoot\config\DDrawCompat-PiratesMoon.ini" "$payloadRoot\config\DDrawCompat-PiratesMoon.ini"
Copy-ReleaseFile "$projectRoot\config\cdaudio-winmm.ini" "$payloadRoot\config\winmm.ini"
Copy-Item -LiteralPath "$projectRoot\shaders\common-shaders-master\mw3-remaster" -Destination "$payloadRoot\shaders\mw3-remaster" -Recurse

# Only the tracks actively mapped by the two games are included.
Copy-ReleaseFile "$projectRoot\music\1-02. MechWarrior 3 - Track 01.mp3" "$payloadRoot\music\mw3\track02.mp3"
Copy-ReleaseFile "$projectRoot\music\1-03. MechWarrior 3 - Track 02.mp3" "$payloadRoot\music\mw3\track03.mp3"
Copy-ReleaseFile "$projectRoot\music\2-01. Pirate's Moon - Track 01.mp3" "$payloadRoot\music\pm\track02.mp3"
Copy-ReleaseFile "$projectRoot\music\2-02. Pirate's Moon - Track 02.mp3" "$payloadRoot\music\pm\track03.mp3"
Copy-ReleaseFile "$projectRoot\music\2-03. Pirate's Moon - Track 03.mp3" "$payloadRoot\music\pm\track04.mp3"

# Original manuals are installed in a clearly named folder and linked from the Start menu.
Copy-ReleaseFile "$releaseRoot\payload\Manuals\MechWarrior 3 Manual.pdf" "$payloadRoot\Manuals\MechWarrior 3 Manual.pdf"
Copy-ReleaseFile "$releaseRoot\payload\Manuals\MechWarrior 3 Pirate's Moon Manual.pdf" "$payloadRoot\Manuals\MechWarrior 3 Pirate's Moon Manual.pdf"

# Credits and upstream licensing/readmes are installed alongside the games.
Copy-ReleaseFile "$releaseRoot\THIRD_PARTY_NOTICES.md" "$payloadRoot\THIRD_PARTY_NOTICES.md"
Copy-ReleaseFile "$releaseRoot\REDISTRIBUTION.md" "$payloadRoot\REDISTRIBUTION.md"
Copy-ReleaseFile "$releaseRoot\payload\README.txt" "$payloadRoot\README.txt"
Copy-ReleaseFile "$projectRoot\tools\DDrawCompat\LICENSE.txt" "$payloadRoot\Third-Party\DDrawCompat-LICENSE.txt"
Copy-ReleaseFile "$projectRoot\tools\DDrawCompat\README.md" "$payloadRoot\Third-Party\DDrawCompat-README.md"
Copy-ReleaseFile "$projectRoot\tools\ZipperFixup\LICENSE" "$payloadRoot\Third-Party\ZipperFixup-LICENSE.txt"
Copy-ReleaseFile "$projectRoot\tools\ZipperFixup\README.md" "$payloadRoot\Third-Party\ZipperFixup-README.md"
Copy-ReleaseFile "$projectRoot\tools\cdaudio-winmm\README.md" "$payloadRoot\Third-Party\cdaudio-winmm-README.md"
Copy-ReleaseFile "$releaseRoot\LICENSE" "$payloadRoot\Third-Party\MW3-Remastered-Installer-LICENSE.txt"

$payloadManifest = Get-ChildItem -LiteralPath $payloadRoot -Recurse -File | Sort-Object FullName | ForEach-Object {
    $relative = $_.FullName.Substring($payloadRoot.Length + 1).Replace('\', '/')
    $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $relative"
}
[IO.File]::WriteAllLines(
    (Join-Path $payloadRoot 'PAYLOAD_MANIFEST.sha256'),
    [string[]]$payloadManifest,
    [Text.UTF8Encoding]::new($false)
)
Copy-Item -LiteralPath (Join-Path $payloadRoot 'PAYLOAD_MANIFEST.sha256') -Destination (Join-Path $distRoot 'PAYLOAD_MANIFEST.sha256') -Force

$forbidden = Get-ChildItem -LiteralPath $payloadRoot -Recurse -Force -File | Where-Object {
    $_.Extension -ieq '.iso' -or $_.Name -ieq '.env' -or $_.Name -like '.env.*'
}
if ($forbidden) { throw "Forbidden release input detected: $($forbidden.FullName -join ', ')" }

$payloadZip = Join-Path $objRoot 'payload.zip'
Compress-Archive -Path (Join-Path $payloadRoot '*') -DestinationPath $payloadZip -CompressionLevel Optimal
$setup = Join-Path $distRoot 'MechWarrior-3-Remastered-Setup.exe'
& $csc /nologo /target:winexe /platform:x64 /optimize+ "/win32manifest:$releaseRoot\src\app.manifest" "/win32icon:$releaseRoot\assets\MW3-Remastered.ico" "/out:$setup" "/resource:$payloadZip,Payload.zip" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll /reference:Microsoft.CSharp.dll "$releaseRoot\src\Installer.cs" "$releaseRoot\src\LauncherRecovery.cs" "$releaseRoot\src\GameInstallRegistry.cs"
if ($LASTEXITCODE) { throw "Installer compilation failed with exit code $LASTEXITCODE." }

$result = Get-Item -LiteralPath $setup
Write-Host "Built $($result.FullName)"
Write-Host ("Size: {0:N1} MB" -f ($result.Length / 1MB))
$setupHash = Get-FileHash -LiteralPath $setup -Algorithm SHA256
$setupHash
[IO.File]::WriteAllText(
    (Join-Path $distRoot 'SHA256SUMS.txt'),
    ($setupHash.Hash.ToLowerInvariant() + '  ' + $result.Name + [Environment]::NewLine),
    [Text.UTF8Encoding]::new($false)
)

# Keep the sanitized duplicate lean: generated payload staging is reproducible.
if (Test-Path -LiteralPath $objRoot) {
    $resolvedObj = (Resolve-Path -LiteralPath $objRoot).Path
    if (-not $resolvedObj.StartsWith($releaseRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe build staging path.' }
    Remove-Item -LiteralPath $resolvedObj -Recurse -Force
}
