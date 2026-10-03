[CmdletBinding()]
param(
    [string]$VcVarsPath = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvarsall.bat'
)
$ErrorActionPreference = 'Stop'
$workspace = Split-Path (Split-Path $PSScriptRoot)
$source = Join-Path $workspace 'tools\ZipperFixup'
$target = Join-Path $workspace 'tools\.build\ZipperFixup-static-crt'
$cargo = Join-Path $workspace 'tools\rust\bin\cargo.exe'
$revision = & git -C $source rev-parse HEAD
if ($LASTEXITCODE -or $revision -ne '1cf586caf9e959e7c4e17a4ba8b63c96711e91a6') { throw 'Unexpected or unreadable ZipperFixup revision.' }
$changes = & git -C $source status --porcelain --untracked-files=no
if ($LASTEXITCODE -or $changes) { throw 'ZipperFixup source is unreadable or has local edits.' }
if ((Get-FileHash (Join-Path $source 'Cargo.lock')).Hash -ne 'E9309A3854DFBC57D2C006CBE0747E453C48CA3F8F7D9B0E589300145E69312E') { throw 'Unexpected Cargo lock.' }
foreach ($path in @($cargo, $VcVarsPath)) { if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing native build prerequisite: $path" } }
$saved = @{}
foreach ($name in @('CARGO_HOME','RUSTUP_HOME','RUSTFLAGS')) { $saved[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
try {
    $env:CARGO_HOME = Join-Path $workspace 'tools\rust'
    $env:RUSTUP_HOME = Join-Path $workspace 'tools\rustup'
    $env:RUSTFLAGS = '-C target-feature=+crt-static -C link-arg=/Brepro'
    if ((& $cargo +1.88.0 --version) -notmatch '^cargo 1\.88\.0 ') { throw 'Rust 1.88.0 is required.' }
    $command = 'call "{0}" x64_x86 && "{1}" +1.88.0 build --offline --locked --release --manifest-path "{2}" --target i686-pc-windows-msvc --target-dir "{3}" -p zippatch -p zipfixup -p export-check' -f $VcVarsPath,$cargo,(Join-Path $source 'Cargo.toml'),$target
    & cmd.exe /d /s /c $command
    if ($LASTEXITCODE) { throw 'Static-runtime native build failed.' }
    $output = Join-Path $target 'i686-pc-windows-msvc\release'
    foreach ($name in @('zippatch.exe','zipfixup.dll')) {
        $report = Join-Path $target ($name + '.dependencies.txt')
        $command = 'call "{0}" x64_x86 >nul && dumpbin /dependents "{1}" > "{2}"' -f $VcVarsPath,(Join-Path $output $name),$report
        & cmd.exe /d /s /c $command
        if ($LASTEXITCODE) { throw 'Native import audit failed.' }
        $imports = @(Get-Content $report | ForEach-Object { if ($_ -match '^\s+([\w.-]+\.dll)\s*$') { $matches[1].ToLowerInvariant() } })
        if (-not $imports.Count -or @($imports | Where-Object { $_ -notin @('kernel32.dll','ntdll.dll','api-ms-win-core-synch-l1-2-0.dll') }).Count) { throw 'Unexpected native runtime dependency.' }
    }
    & (Join-Path $output 'export-check.exe') (Join-Path $output 'zipfixup.dll')
    if ($LASTEXITCODE) { throw 'ZipperFixup export verification failed.' }
    foreach ($name in @('zippatch.exe','zipfixup.dll')) { Get-FileHash (Join-Path $output $name) }
}
finally {
    foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name, $saved[$name], 'Process') }
}
