$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$root=Join-Path $repo ('.local/runner-contract-'+[guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($root)
$fake=Join-Path $root 'FailCompiler.cmd'
[IO.File]::WriteAllText($fake,"@exit /b 13`r`n",[Text.Encoding]::ASCII)
$report=Join-Path $root 'failure.json'
$failed=$false
try { & (Join-Path $PSScriptRoot 'Invoke-SourceRegression.ps1') -CompilerPath $fake -ReportPath $report } catch { $failed=$true }
if(-not $failed){throw 'Compile failure was reported as success'}
$r=Get-Content $report -Raw|ConvertFrom-Json
if($r.Passed -or @($r.Checks).Count -ne 0 -or $r.Failure.Check -ne 'GameInstallRegistry-compile' -or $r.Failure.Message -notmatch '13'){throw 'Compile failure evidence missing or misleading'}
$missing=Join-Path $root 'missing.json'
try { & (Join-Path $PSScriptRoot 'Invoke-SourceRegression.ps1') -CompilerPath (Join-Path $root 'absent.exe') -ReportPath $missing; throw 'Missing compiler accepted' } catch { if($_.Exception.Message -eq 'Missing compiler accepted'){throw} }
$r=Get-Content $missing -Raw|ConvertFrom-Json
if($r.Passed -or $r.Failure.Check -ne 'compiler-preflight'){throw 'Preflight failure evidence missing'}
$successReport=Join-Path $root 'success.json'
$runner=(Join-Path $PSScriptRoot 'Invoke-SourceRegression.ps1').Replace("'", "''")
$successPath=$successReport.Replace("'", "''")
$command="& '$runner' -ReportPath '$successPath'; exit `$LASTEXITCODE"
$encoded=[Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))
$shell=(Get-Process -Id $PID).Path
& $shell -NoProfile -EncodedCommand $encoded
if($LASTEXITCODE -ne 0){throw 'Successful gate leaked an expected negative-probe exit code to its calling shell'}
$r=Get-Content $successReport -Raw|ConvertFrom-Json
if(-not $r.Passed -or @($r.Checks).Count -ne 9){throw 'Successful subprocess did not complete the source gate'}
$global:LASTEXITCODE=0
'Source regression runner failure contracts passed'
