Set-StrictMode -Version Latest

function Get-InstallerSourceFiles([string]$Root) {
    $files = [string[]]@(
        foreach ($relative in @('Installer/Russian', 'tests/Installer.Tests', 'tools/installer-check')) {
            Get-ChildItem -LiteralPath (Join-Path $Root $relative) -Recurse -File |
                Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } | ForEach-Object { $_.FullName }
        }
        foreach ($relative in @('scripts/Build-Installer.ps1', 'scripts/Installer-Common.ps1', 'scripts/Update-ReleaseInstaller.ps1', 'scripts/Publish-Release.ps1', 'scripts/Local-Common.ps1', 'LICENSE', 'THIRD-PARTY-NOTICES.md')) {
            (Join-Path $Root $relative)
        }
    )
    [Array]::Sort($files, [StringComparer]::Ordinal)
    $files
}

function Get-InstallerFingerprint([string]$Root) {
    $lines = @(Get-InstallerSourceFiles $Root | ForEach-Object {
        $_.Substring($Root.Length + 1).Replace('\', '/') + ':' + (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash
    })
    $sha = [Security.Cryptography.SHA256]::Create()
    try { ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes(($lines -join "`n"))))).Replace('-', '') }
    finally { $sha.Dispose() }
}

function Assert-InstallerBuild([string]$Root) {
    $info = Get-Content -LiteralPath (Join-Path $Root 'artifacts/installer-build-info.json') -Raw | ConvertFrom-Json
    if ((Get-InstallerFingerprint $Root) -ne $info.SourceFingerprint) { throw 'Installer sources changed. Run Build-Installer.ps1.' }
    if ((Get-FileHash -LiteralPath $info.Exe -Algorithm SHA256).Hash -ne $info.SHA256 -or
        (Get-FileHash -LiteralPath $info.SourceArchive -Algorithm SHA256).Hash -ne $info.SourceSHA256) {
        throw 'Installer files differ from the verified build.'
    }
    $info
}
