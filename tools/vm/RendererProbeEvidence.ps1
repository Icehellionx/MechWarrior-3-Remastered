Set-StrictMode -Version Latest

function Read-RendererProbeEvidence {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$LogPath,
        [Parameter(Mandatory)][datetime]$StartedUtc,
        [Parameter(Mandatory)][bool]$Exited,
        [Nullable[int]]$ExitCode,
        [ValidateSet('none','close','kill')][string]$Cleanup = 'none'
    )
    $result = [ordered]@{
        LogStatus = 'missing'; Driver = $null; HookFailure = $false
        TagFailure = $false; PrimaryFailure = $false
        ProcessObservation = $(if ($Exited) { 'early-exit' } else { 'alive-at-observation' })
        ExitCode = $ExitCode; Cleanup = $Cleanup; GameplayQualified = $false
    }
    if (-not (Test-Path -LiteralPath $LogPath -PathType Leaf)) { return [pscustomobject]$result }
    $file = Get-Item -LiteralPath $LogPath
    if ($file.LastWriteTimeUtc -lt $StartedUtc.ToUniversalTime()) {
        $result.LogStatus = 'stale'; return [pscustomobject]$result
    }
    if ($file.Length -gt 4MB) { $result.LogStatus = 'oversized'; return [pscustomobject]$result }
    $stream = [IO.File]::Open($file.FullName, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
    try {
        $reader = New-Object IO.StreamReader($stream)
        try { $text = $reader.ReadToEnd() } finally { $reader.Dispose() }
    } finally { $stream.Dispose() }
    $result.LogStatus = 'current'
    # Configuration names do not establish a loaded driver. Return only its fixed file name.
    $match = [regex]::Match($text, '(?im)^.*Hooking user mode display driver:\s*[^\r\n]*[\\/]([^\\/\r\n]+\.dll)(?:\+[^\r\n]*)?\s*$')
    if ($match.Success) { $result.Driver = $match.Groups[1].Value }
    $result.HookFailure = $text.Contains('Failed to create a render target for hooking')
    $result.TagFailure = $text.Contains('TagSurface not found')
    $result.PrimaryFailure = [regex]::IsMatch($text, 'primary[^\r\n]*FAILED:')
    return [pscustomobject]$result
}
