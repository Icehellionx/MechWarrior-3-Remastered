Set-StrictMode -Version Latest

function Invoke-InstallerMethod {
    param(
        [Parameter(Mandatory)]$InstallerType,
        [Parameter(Mandatory)]$BindingFlags,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][object[]]$Arguments
    )

    $method = $InstallerType.GetMethod($Name, $BindingFlags)
    if (-not $method) { throw "Installer smoke contract could not find method: $Name" }
    $method.Invoke($null, $Arguments)
}

function Invoke-WithMountedIso {
    param(
        [Parameter(Mandatory)]$Assembly,
        [Parameter(Mandatory)][string]$IsoPath,
        [Parameter(Mandatory)][scriptblock]$Action
    )

    $resolvedIso = [string](Resolve-Path -LiteralPath $IsoPath).Path
    $mountType = $Assembly.GetType('IsoMountSession')
    if (-not $mountType) { throw 'Installer smoke contract could not find IsoMountSession.' }
    $logger = [Action[string]]{ param($message) Write-Host $message }
    $session = $mountType.GetMethod('Attach').Invoke($null, [object[]]@($resolvedIso, $logger))
    try {
        $discRoot = [string]$mountType.GetProperty('Root').GetValue($session, $null)
        if (-not (Test-Path -LiteralPath $discRoot -PathType Container)) {
            throw 'Mounted ISO did not expose a readable disc root.'
        }
        & $Action $discRoot
    }
    finally {
        if ($session) { $session.Dispose() }
    }
}

function Install-DiscGameSmoke {
    param(
        [Parameter(Mandatory)]$InstallerType,
        [Parameter(Mandatory)]$BindingFlags,
        [Parameter(Mandatory)][string]$DiscRoot,
        [Parameter(Mandatory)][string]$GameRoot,
        [Parameter(Mandatory)][string]$Extractor,
        [Parameter(Mandatory)][string]$Payload,
        [Parameter(Mandatory)][bool]$PiratesMoon
    )

    Invoke-InstallerMethod $InstallerType $BindingFlags 'ExtractGame' ([object[]]@($DiscRoot, $GameRoot, $Extractor))
    Invoke-InstallerMethod $InstallerType $BindingFlags 'CopyVideo' ([object[]]@($DiscRoot, $GameRoot))
    if ($PiratesMoon) {
        Invoke-InstallerMethod $InstallerType $BindingFlags 'EnsurePiratesMoonRuntimeFiles' ([object[]]@($GameRoot))
        $mediaType = $InstallerType.Assembly.GetType('PiratesMoonMedia')
        $patchMethod = $mediaType.GetMethod('RemoveRuntimeDiscCheck', $BindingFlags)
        $patchMethod.Invoke($null, [object[]]@([string](Join-Path $GameRoot 'Mech3.exe')))
    }
    else {
        Invoke-InstallerMethod $InstallerType $BindingFlags 'ApplyPatch12' ([object[]]@([string](Join-Path $Payload 'patch12'), $GameRoot))
        $leveler = $InstallerType.Assembly.GetType('SoundArchiveLeveler')
        $apply = $leveler.GetMethod('ApplyToStagedGame', $BindingFlags)
        $apply.Invoke($null, [object[]]@([string]$GameRoot))
    }
    Invoke-InstallerMethod $InstallerType $BindingFlags 'InstallCompatibility' ([object[]]@($Payload, $GameRoot, $PiratesMoon))
}

function Assert-InstalledGameSmoke {
    param(
        [Parameter(Mandatory)][string]$GameRoot,
        [Parameter(Mandatory)][bool]$PiratesMoon,
        [Parameter(Mandatory)][string]$QualifiedDdrawHash,
        [switch]$RequireDiscVideo
    )

    $gameName = if ($PiratesMoon) { "Pirate's Moon" } else { 'MechWarrior 3' }
    $required = @(
        'Mech3.exe', 'Mech3fixup.exe', 'ddraw.dll', 'zipfixup.dll', 'winmm.dll',
        'IFORCE2.dll', 'force_eff.ifr', 'mcicda\cdaudioplr.exe', 'mcicda\NLayer.dll',
        'common-shaders-master\mw3-remaster\mw3-remaster.cg'
    )
    if ($PiratesMoon) { $required += @('Mech3Msg.dll', 'DATA.TAG') }
    else {
        $expected = @{
            'zbd\soundsH.zbd' = '20AD72D51EAFFB4447A7CE09B408B017CFAA5A7034A82E73B85B539355579BCB'
            'zbd\soundsL.zbd' = '612F8EDB1E26884AAD04E34F3E73D41D7661F8652F97EB765C8C28EAC9A37D98'
            'OriginalSoundArchives\soundsH.zbd' = '71C4688E38D59E03D3E0A63C8EF90CCE0359103890904AAAB5DC461647F484A4'
            'OriginalSoundArchives\soundsL.zbd' = 'E259704B36069339BAD035AE571F7733A6655ED2020C4D7E24B5C8872A3B09C3'
        }
        foreach ($relative in $expected.Keys) {
            $actual = (Get-FileHash -LiteralPath (Join-Path $GameRoot $relative) -Algorithm SHA256).Hash
            if ($actual -ne $expected[$relative]) { throw "MW3 sound archive mismatch: $relative" }
        }
    }
    $tracks = if ($PiratesMoon) { @('track02.mp3', 'track03.mp3', 'track04.mp3') } else { @('track02.mp3', 'track03.mp3') }
    $required += $tracks | ForEach-Object { "mcicda\music\$_" }
    foreach ($relative in $required) {
        if (-not (Test-Path -LiteralPath (Join-Path $GameRoot $relative) -PathType Leaf)) {
            throw "$gameName smoke output is missing: $relative"
        }
    }
    if ($PiratesMoon) {
        $expectedDataTag = "[TagInfo]`r`nCompany=Microprose`r`nApplication=MechWarrior 3 Pirate's Moon`r`nVersion=1.0`r`nCategory=Games`r`nMisc=`r`n"
        $actualDataTag = [IO.File]::ReadAllText((Join-Path $GameRoot 'DATA.TAG'), [Text.Encoding]::ASCII)
        if ($actualDataTag -cne $expectedDataTag) { throw "$gameName DATA.TAG content is invalid." }
    }

    $keys = Join-Path $GameRoot 'keys'
    if (-not (Test-Path -LiteralPath $keys -PathType Container)) { throw "$gameName control-profile storage is missing." }
    $writeProbe = Join-Path $keys '.smoke-write-test.tmp'
    try { [IO.File]::WriteAllText($writeProbe, 'writable') }
    finally { if (Test-Path -LiteralPath $writeProbe) { Remove-Item -LiteralPath $writeProbe -Force } }

    if ($RequireDiscVideo) {
        $video = Join-Path $GameRoot 'video'
        if (-not (Test-Path -LiteralPath $video -PathType Container) -or
            -not (Get-ChildItem -LiteralPath $video -Recurse -File | Select-Object -First 1)) {
            throw "$gameName disc installation did not copy a populated video directory."
        }
    }

    $config = Get-Content -LiteralPath (Join-Path $GameRoot 'DDrawCompat.ini') -Raw
    $commonSettings = @(
        'FullscreenMode = borderless', 'AltTabFix = keepvidmem(1)', 'DisplayAspectRatio = 4:3',
        'ResolutionScale = display(1)', 'ResolutionScaleFilter = bilinear',
        'SupportedResolutions = 640x480, 1024x768', 'Antialiasing = msaa4x(0)',
        'TextureFilter = af16x', 'LogLevel = info'
    )
    foreach ($setting in $commonSettings) {
        if (-not $config.Contains($setting)) { throw "$gameName renderer profile is missing: $setting" }
    }

    $baseOnlySettings = @('CpuAffinityRotation = off', 'RemasterIntroWidescreen = on',
        'RemasterIntroChromaCleanup = on', 'PresentationEdgeRepair = 4')
    if ($PiratesMoon) {
        foreach ($setting in @('VSync = on', 'PresentDelay = on(50)')) {
            if (-not $config.Contains($setting)) { throw "$gameName renderer profile is missing: $setting" }
        }
        foreach ($setting in $baseOnlySettings) {
            if ($config.Contains($setting.Split('=')[0].Trim() + ' =')) { throw "$gameName contains base-only setting: $setting" }
        }
        if (Test-Path -LiteralPath (Join-Path $GameRoot 'CRACK')) { throw "$gameName smoke output retained the CRACK directory." }
        if (Test-Path -LiteralPath (Join-Path $GameRoot 'CLASS.NFO.txt')) { throw "$gameName smoke output retained the NFO." }
    }
    else {
        foreach ($setting in $baseOnlySettings) {
            if (-not $config.Contains($setting)) { throw "$gameName renderer profile is missing: $setting" }
        }
    }

    if ($config.Contains('RemasterStartupSurfaceClear') -or $config.Contains('RemasterTexture') -or $config.Contains('RemasterTelemetry')) {
        throw "$gameName renderer profile contains a disproven or unreleased hook."
    }
    $actualDdrawHash = (Get-FileHash -LiteralPath (Join-Path $GameRoot 'ddraw.dll') -Algorithm SHA256).Hash
    if ($actualDdrawHash -ne $QualifiedDdrawHash) { throw "$gameName contains an unqualified ddraw.dll: $actualDdrawHash" }

    $expectedExeHash = if ($PiratesMoon) {
        'B28ECB70A6A5AFC01074C0ED32BFDABBD189EB3630CB2CDC528107CE67CAEA0E'
    } else {
        '95BC2C114C9B2E5C5ADA8E40CA78CC79470F862E45313FFAD0D1E8E6D4D916BF'
    }
    $actualExeHash = (Get-FileHash -LiteralPath (Join-Path $GameRoot 'Mech3.exe') -Algorithm SHA256).Hash
    if ($actualExeHash -ne $expectedExeHash) { throw "$gameName installed executable hash is unexpected: $actualExeHash" }
}

function Invoke-InstalledAudioSmoke {
    param(
        [Parameter(Mandatory)][string]$LifecycleTest,
        [Parameter(Mandatory)][string]$ProtocolTest,
        [Parameter(Mandatory)][string]$GameRoot,
        [Parameter(Mandatory)][int]$LastTrack,
        [Parameter(Mandatory)][string]$GameName,
        [switch]$VerifyPlayback
    )

    & $LifecycleTest $GameRoot
    if ($LASTEXITCODE) { throw "$GameName CD-audio lifecycle test failed with exit code $LASTEXITCODE." }
    $protocolArguments = @((Join-Path $GameRoot 'mcicda\cdaudioplr.exe'), $LastTrack)
    if ($VerifyPlayback) { $protocolArguments += 'playback' }
    & $ProtocolTest @protocolArguments
    if ($LASTEXITCODE) { throw "$GameName CD-audio protocol test failed with exit code $LASTEXITCODE." }
}
