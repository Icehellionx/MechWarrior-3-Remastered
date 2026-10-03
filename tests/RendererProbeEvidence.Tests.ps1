$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../tools/vm/RendererProbeEvidence.ps1')
$root = Join-Path ([IO.Path]::GetTempPath()) ('mw3-evidence-' + [guid]::NewGuid())
[void][IO.Directory]::CreateDirectory($root)
$path = Join-Path $root 'probe.log'
$started = [datetime]::UtcNow.AddMinutes(-1)
$count = 0
function Assert($condition, $message) { if (-not $condition) { throw $message }; $script:count++ }
try {
    $r = Read-RendererProbeEvidence $path $started $true -ExitCode 7
    Assert ($r.LogStatus -eq 'missing' -and $r.ExitCode -eq 7) 'Missing log or exit code lost'
    [IO.File]::WriteAllText($path, '[process] ForceD3D9On12 = on')
    $r = Read-RendererProbeEvidence $path $started $true
    Assert ($null -eq $r.Driver -and -not $r.GameplayQualified) 'Option name falsely proves driver/gameplay'
    Assert ($r.ProcessObservation -eq 'early-exit') 'Early exit not tracked'
    $writer = [IO.File]::Open($path, [IO.FileMode]::Open, [IO.FileAccess]::Write, [IO.FileShare]::Read)
    try {
        $r = Read-RendererProbeEvidence $path $started $false
        Assert ($r.LogStatus -eq 'current') 'Live writer prevents evidence collection'
    } finally { $writer.Dispose() }
    [IO.File]::WriteAllText($path, "Hooking user mode display driver: C:\PrivateUser\VBoxDispD3D-x86.dll+0x1b20`r`nERROR: Failed to create a render target for hooking: 0x8876086c`r`nERROR: TagSurface not found`r`nUsing resource: primary, anymem (FAILED: 0x80004005)")
    $r = Read-RendererProbeEvidence $path $started $false -Cleanup kill
    Assert ($r.Driver -eq 'VBoxDispD3D-x86.dll') 'Actual driver file name not recognized'
    Assert ($r.HookFailure -and $r.TagFailure -and $r.PrimaryFailure) 'Renderer failures missing'
    Assert ($r.Cleanup -eq 'kill' -and $r.ProcessObservation -eq 'alive-at-observation' -and -not $r.GameplayQualified) 'Process liveness qualified gameplay or cleanup lost'
    (Get-Item $path).LastWriteTimeUtc = $started.AddSeconds(-1)
    $r = Read-RendererProbeEvidence $path $started $false
    Assert ($r.LogStatus -eq 'stale' -and $null -eq $r.Driver -and -not $r.HookFailure) 'Stale log contaminated result'
    [IO.File]::WriteAllText($path, ('x' * (4MB + 1)))
    $r = Read-RendererProbeEvidence $path $started $false
    Assert ($r.LogStatus -eq 'oversized') 'Oversized log accepted'
    [IO.File]::WriteAllText($path, '')
    $r = Read-RendererProbeEvidence $path $started $false
    Assert ($r.LogStatus -eq 'current' -and -not $r.GameplayQualified) 'Empty/current log qualified gameplay'
    "Renderer evidence smoke/regressions passed: $count assertions"
} finally {
    Remove-Item -LiteralPath $path -ErrorAction SilentlyContinue
    [IO.Directory]::Delete($root)
}
