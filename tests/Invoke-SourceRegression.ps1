[CmdletBinding()]
param(
    [string]$CompilerPath = (Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'),
    [string]$ReportPath,
    [switch]$IncludeLauncherParity
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$output = Join-Path $repo ('.local/source-regression-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($output)
if (-not $ReportPath) { $ReportPath = Join-Path $output 'results.json' }
$report = [ordered]@{StartedUtc=[datetime]::UtcNow.ToString('o');Passed=$false;Checks=@();Failure=$null}
$current = 'compiler-preflight'
try {
    if (-not (Test-Path -LiteralPath $CompilerPath -PathType Leaf)) { throw 'C# compiler unavailable' }
    $cases = @(
        @{Name='GameInstallRegistry';Sources=@('GameInstallRegistry')},
        @{Name='GameControlStorage';Sources=@('GameControlStorage')},
        @{Name='GameSaveStorage';Sources=@('GameSaveStorage')},
        @{Name='LauncherShortcutPolicy';Sources=@('LauncherShortcutPolicy','InstalledShellLink')},
        @{Name='LauncherRecovery';Sources=@('LauncherRecovery','InstalledProcessScope')},
        @{Name='PiratesMoonMedia';Sources=@('PiratesMoonMedia')}
    )
    foreach ($case in $cases) {
        $current = $case.Name + '-compile'
        $exe = Join-Path $output ($case.Name + 'Smoke.exe')
        $sources = @((Join-Path $PSScriptRoot ($case.Name+'Smoke.cs')))
        $sources += @($case.Sources | ForEach-Object { Join-Path $repo ('src/'+$_+'.cs') })
        & $CompilerPath /nologo /target:exe /platform:anycpu /reference:Microsoft.CSharp.dll "/out:$exe" @sources
        if ($LASTEXITCODE -ne 0) { throw "$current failed with exit code $LASTEXITCODE" }
        $current = $case.Name + '-run'
        & $exe
        if ($LASTEXITCODE -ne 0) { throw "$current failed with exit code $LASTEXITCODE" }
        $report.Checks += [ordered]@{Name=$case.Name;Passed=$true}
    }
    $current = 'RendererProbeEvidence'
    & (Join-Path $PSScriptRoot 'RendererProbeEvidence.Tests.ps1')
    $report.Checks += [ordered]@{Name=$current;Passed=$true}
    $current = 'PackagedFolderInstallHarness-compile'
    $harness = Join-Path $output 'PackagedFolderInstallSmoke.exe'
    & $CompilerPath /nologo /target:exe /platform:x64 /reference:System.Windows.Forms.dll "/out:$harness" (Join-Path $PSScriptRoot 'PackagedFolderInstallSmoke.cs')
    if ($LASTEXITCODE -ne 0) { throw "$current failed with exit code $LASTEXITCODE" }
    $current = 'PackagedFolderInstallHarness-guard'
    & $harness
    if ($LASTEXITCODE -ne 2) { throw 'Packaged installer harness accepted an unguarded invocation' }
    $report.Checks += [ordered]@{Name=$current;Passed=$true}
    $current = 'PackagedFolderInstallHarness-missing-identity'
    $savedLabGuard = $env:MW3_WINE_TEST
    $savedSetupIdentity = $env:MW3_TEST_SETUP_SHA256
    try {
        $env:MW3_WINE_TEST = '1'
        $env:MW3_TEST_SETUP_SHA256 = $null
        $refusal = & $harness 'C:\MW3Lab\MissingSetup.exe' 'D:\' 'C:\MW3Lab\WineMissingIdentity' 2>&1
        if ($LASTEXITCODE -ne 1 -or ($refusal -join "`n") -notmatch 'Unqualified setup identity') { throw 'Packaged harness did not refuse a missing expected identity before loading setup' }
    } finally {
        $env:MW3_WINE_TEST = $savedLabGuard
        $env:MW3_TEST_SETUP_SHA256 = $savedSetupIdentity
    }
    $report.Checks += [ordered]@{Name=$current;Passed=$true}
    if ($IncludeLauncherParity) {
        $current = 'LauncherParity'
        & (Join-Path $PSScriptRoot 'Invoke-LauncherParitySmoke.ps1')
        $report.Checks += [ordered]@{Name=$current;Passed=$true}
    }
    $report.Passed = $true
} catch {
    $report.Failure = [ordered]@{Check=$current;Message=$_.Exception.Message}
    throw
} finally {
    $report.CompletedUtc = [datetime]::UtcNow.ToString('o')
    $report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $ReportPath
}
# Expected refusal probes return nonzero. Clear their status only after every
# assertion passed so the calling CI shell observes this gate's success.
$global:LASTEXITCODE = 0
