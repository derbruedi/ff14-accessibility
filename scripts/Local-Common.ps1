# Common functions for the local Windows build/package scripts.
Set-StrictMode -Version Latest

function Get-LocalRoot {
    [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
}

function Get-LocalSourceFingerprint([string]$Root) {
    $source = Join-Path $Root 'FF14Accessibility'
    $files = [string[]]@(Get-ChildItem -LiteralPath $source -File -Recurse |
        Where-Object { $_.FullName.Substring($source.Length) -notmatch '[\\/](bin|obj)[\\/]' } |
        ForEach-Object { $_.FullName })
    # Explicit ordinal ordering is identical in Windows PowerShell 5.1 and PowerShell 7.
    [Array]::Sort($files, [StringComparer]::Ordinal)
    $lines = @($files | ForEach-Object {
            $_.Substring($Root.Length + 1).Replace('\', '/') + ':' +
                (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash
        })
    foreach ($name in @('LICENSE', 'THIRD-PARTY-NOTICES.md')) {
        $lines += $name + ':' + (Get-FileHash -LiteralPath (Join-Path $Root $name) -Algorithm SHA256).Hash
    }
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes(($lines -join "`n"))))).Replace('-', '')
    } finally { $sha.Dispose() }
}

function Get-LocalVersion([string]$Root) {
    [xml]$project = Get-Content -LiteralPath (Join-Path $Root 'FF14Accessibility\FF14Accessibility.csproj') -Raw
    $group = @($project.Project.PropertyGroup | Where-Object { $_.PSObject.Properties['Version'] })[0]
    [string]$group.Version
}

# Includes the tests and the verification tools, so changing a check invalidates
# an earlier success just like changing the plugin does.
function Get-LocalVerificationFingerprint([string]$Root) {
    $lines = @('Plugin:' + (Get-LocalSourceFingerprint $Root))
    $files = [string[]]@(foreach ($directory in @('tests', 'scripts', 'Installer\Russian')) {
        Get-ChildItem -LiteralPath (Join-Path $Root $directory) -File -Recurse |
            Where-Object { $_.FullName -notmatch '[\\/](bin|obj|TestResults)[\\/]' } |
            ForEach-Object { $_.FullName }
    })
    # The pinned author hashes are verification input, not ordinary prose.
    $files = [string[]]@($files + (Join-Path $Root 'docs\maintenance\AUTHOR_NAVIGATION.json') +
        (Join-Path $Root 'tools\nav-route-check\NavigationPathCheck.cs'))
    [Array]::Sort($files, [StringComparer]::Ordinal)
    foreach ($file in $files) {
        $lines += $file.Substring($Root.Length + 1).Replace('\', '/') + ':' +
            (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
    }
    $sha = [Security.Cryptography.SHA256]::Create()
    try { ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes(($lines -join "`n"))))).Replace('-', '') }
    finally { $sha.Dispose() }
}

function Assert-LocalVerification([string]$Root) {
    $path = Join-Path $Root 'artifacts\test-info.json'
    if (!(Test-Path -LiteralPath $path)) { throw 'No test report. Run Test-Local.ps1 before packaging.' }
    $report = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    if (!$report.Passed -or $report.Total -le 0 -or $report.Skipped -ne 0) {
        throw 'Tests did not pass completely. Run Test-Local.ps1.'
    }
    if ($report.VerificationFingerprint -ne (Get-LocalVerificationFingerprint $Root)) {
        throw 'Plugin, installer, tests or verification tools changed since testing. Run Test-Local.ps1 again.'
    }
}

function Assert-LocalPlugin([string]$Directory, [string]$Version) {
    foreach ($name in @('FF14Accessibility.dll', 'FF14Accessibility.json', 'FF14Accessibility.deps.json',
        'Tolk.dll', 'nvdaControllerClient64.dll', 'System.Speech.dll', 'NAudio.dll', 'NAudio.Core.dll',
        'NAudio.WinMM.dll', 'NAudio.Wasapi.dll', 'LICENSE', 'THIRD-PARTY-NOTICES.md')) {
        if (!(Test-Path -LiteralPath (Join-Path $Directory $name) -PathType Leaf)) { throw "Missing package file: $name" }
    }
    $dll = Join-Path $Directory 'FF14Accessibility.dll'
    $actual = [Reflection.AssemblyName]::GetAssemblyName($dll).Version
    $expected = [version]($Version + '.0')
    $manifest = Get-Content -LiteralPath (Join-Path $Directory 'FF14Accessibility.json') -Raw | ConvertFrom-Json
    if ($actual -ne $expected -or [version]$manifest.AssemblyVersion -ne $expected) {
        throw "DLL/manifest version mismatch: expected $expected, DLL $actual, manifest $($manifest.AssemblyVersion)"
    }
    $sounds = @(Get-ChildItem -LiteralPath (Join-Path $Directory 'assets\partymonitor') -Filter '*.mp3' -File)
    if ($sounds.Count -ne 122) { throw "Unexpected party monitor sound count: $($sounds.Count)" }
    $speech = Join-Path $Directory 'runtimes\win\lib\net9.0\System.Speech.dll'
    if (!(Test-Path -LiteralPath $speech) -or
        (Get-FileHash -LiteralPath $speech).Hash -ne (Get-FileHash -LiteralPath (Join-Path $Directory 'System.Speech.dll')).Hash) {
        throw 'Windows speech runtime was not flattened correctly.'
    }
    $forbidden = @(Get-ChildItem -LiteralPath $Directory -File -Recurse | Where-Object {
        $_.Extension -in @('.pdb', '.cs', '.csproj', '.sln', '.zip', '.log', '.bak') -or
        $_.Name -in @('Dalamud.dll', 'FFXIVClientStructs.dll', 'Lumina.dll', 'Lumina.Excel.dll')
    })
    if ($forbidden.Count) { throw "Unexpected files in runtime package: $($forbidden.Name -join ', ')" }
}
