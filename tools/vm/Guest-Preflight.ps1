# Read-only Windows guest capability and optional installed-tree/transfer probe.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OutputPath,
    [string]$GameRoot,
    [string]$InputPath
)

$ErrorActionPreference = 'Stop'
$os = Get-CimInstance Win32_OperatingSystem
$video = @(Get-CimInstance Win32_VideoController | ForEach-Object {
    [pscustomobject]@{ Name = $_.Name; DriverVersion = $_.DriverVersion }
})
$screen = $null
try {
    Add-Type -AssemblyName System.Windows.Forms
    $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $screen = [pscustomobject]@{ Width = $bounds.Width; Height = $bounds.Height }
} catch {
    $screen = [pscustomobject]@{ Error = $_.Exception.Message }
}
$files = @('Mech3.exe', 'Mech3fixup.exe', 'ddraw.dll', 'zipfixup.dll', 'winmm.dll')
$installed = $null
if ($GameRoot) {
    $installed = [ordered]@{ RootExists = (Test-Path -LiteralPath $GameRoot -PathType Container) }
    foreach ($name in $files) {
        $installed[$name] = Test-Path -LiteralPath (Join-Path $GameRoot $name) -PathType Leaf
    }
}
$inputFile = $null
if ($InputPath) {
    $inputFile = [ordered]@{ Exists = (Test-Path -LiteralPath $InputPath -PathType Leaf) }
    if ($inputFile.Exists) {
        $inputFile['Sha256'] = (Get-FileHash -LiteralPath $InputPath -Algorithm SHA256).Hash
        $inputFile['Length'] = (Get-Item -LiteralPath $InputPath).Length
    }
}
$audioService = Get-Service -Name Audiosrv -ErrorAction SilentlyContinue
$report = [ordered]@{
    GeneratedUtc = (Get-Date).ToUniversalTime().ToString('o')
    Computer = $env:COMPUTERNAME
    OsCaption = $os.Caption
    OsVersion = $os.Version
    Architecture = $os.OSArchitecture
    Video = $video
    Screen = $screen
    PowerShell32 = (Test-Path -LiteralPath "$env:WINDIR\SysWOW64\WindowsPowerShell\v1.0\powershell.exe" -PathType Leaf)
    AudioService = if ($audioService) { $audioService.Status.ToString() } else { 'Missing' }
    Game = $installed
    InputFile = $inputFile
}
$directory = Split-Path -Parent $OutputPath
if ($directory) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
Write-Output "MW3 guest preflight written: $OutputPath"