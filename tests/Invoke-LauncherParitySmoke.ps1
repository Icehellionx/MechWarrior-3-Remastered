[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$releaseRoot = Split-Path -Parent $PSScriptRoot
$outputRoot = Join-Path $releaseRoot '.local'
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$output = Join-Path $outputRoot 'LauncherParitySmoke.exe'
$sources = @('Launcher', 'LauncherRuntime', 'LauncherRecovery', 'InstalledProcessScope', 'GameInstallRegistry', 'GameControlStorage', 'LauncherHelpForm', 'InstalledDocument', 'InstallationDiagnostics', 'DiagnosticsForm', 'GraphicsProfileService', 'GraphicsSettingsForm') | ForEach-Object { Join-Path $releaseRoot "src\$_.cs" }
& $csc /nologo /target:exe /platform:anycpu /main:LauncherParitySmoke /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll "/out:$output" (Join-Path $PSScriptRoot 'LauncherParitySmoke.cs') @sources
if ($LASTEXITCODE) { throw 'Launcher parity compilation failed.' }
& $output $releaseRoot
if ($LASTEXITCODE) { throw 'Launcher parity contracts failed.' }
