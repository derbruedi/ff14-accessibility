[CmdletBinding()]
param(
    [string]$ArchivePath,
    [Parameter(Mandatory = $true)][string]$NotesFile,
    [string]$InstallerBuildRoot,
    [switch]$InstallerOnly,
    [switch]$UpdateExisting,
    [switch]$CheckOnly
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Local-Common.ps1')
. (Join-Path $PSScriptRoot 'Installer-Common.ps1')
$root = Get-LocalRoot
$repository = 'pitpamyati-netizen/ff14-accessibility'

# Updating only the installer in the existing latest release is explicitly
# separate from publishing a new game-plugin version or changing Git state.
if ($InstallerOnly) {
    & (Join-Path $PSScriptRoot 'Update-ReleaseInstaller.ps1') -NotesFile $NotesFile -CheckOnly:$CheckOnly
    return
}
if ([string]::IsNullOrWhiteSpace($ArchivePath)) { throw 'ArchivePath is required for a new plugin release.' }

function Invoke-ReleaseCommand([string]$Command, [string[]]$Arguments) {
    $output = & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Command failed: $Command $($Arguments -join ' ')" }
    $output
}

Push-Location $root
try {
    $archive = (Resolve-Path -LiteralPath $ArchivePath).Path
    $notes = (Resolve-Path -LiteralPath $NotesFile).Path
    if ([string]::IsNullOrWhiteSpace([IO.File]::ReadAllText($notes))) { throw 'Release notes are empty.' }
    $dirty = Invoke-ReleaseCommand 'git' @('status', '--porcelain=v1', '--untracked-files=all')
    if ($dirty) { throw 'Commit all source and documentation changes before publishing.' }
    $origin = Invoke-ReleaseCommand 'git' @('remote', 'get-url', 'origin')
    if ($origin -notin @("https://github.com/$repository.git", "https://github.com/$repository", "git@github.com:$repository.git")) {
        throw 'origin is not the Russian fork. Refusing to publish to another repository.'
    }
    $head = Invoke-ReleaseCommand 'git' @('rev-parse', 'HEAD')
    $branch = Invoke-ReleaseCommand 'git' @('symbolic-ref', '--quiet', '--short', 'HEAD')
    $remote = Invoke-ReleaseCommand 'git' @('ls-remote', '--exit-code', 'origin', "refs/heads/$branch")
    if (($remote -split '\s+')[0] -ne $head) { throw 'Push the current branch before publishing.' }

    $version = Get-LocalVersion $root
    # Reuse an independently verified installer with its full source asset.
    # This avoids mixing unrelated installer work into the plugin commit.
    $installerRoot = if ([string]::IsNullOrWhiteSpace($InstallerBuildRoot)) { $root } else {
        (Resolve-Path -LiteralPath $InstallerBuildRoot).Path
    }
    $installer = & {
        param($verifiedRoot)
        . (Join-Path $verifiedRoot 'scripts/Installer-Common.ps1')
        Assert-InstallerBuild $verifiedRoot
    } $installerRoot
    $tag = "v$version"
    $existingTag = Invoke-ReleaseCommand 'git' @('ls-remote', '--tags', 'origin', "refs/tags/$tag", "refs/tags/$tag^{}")
    if ($existingTag) { throw "Tag $tag already exists. Use a new version; do not replace a published release." }
    $info = Get-Content -LiteralPath (Join-Path $root 'artifacts/build-info.json') -Raw | ConvertFrom-Json
    $report = Get-Content -LiteralPath ($archive + '.verification.json') -Raw | ConvertFrom-Json
    $plugin = Join-Path $root 'artifacts/plugin'
    Assert-LocalPlugin $plugin $version
    if ($info.Version -ne $version -or (Get-LocalSourceFingerprint $root) -ne $info.SourceFingerprint) {
        throw 'Source differs from the build. Run Build-Local.ps1, Test-Local.ps1 and Package-Local.ps1 again.'
    }
    $dllHash = (Get-FileHash -LiteralPath (Join-Path $plugin 'FF14Accessibility.dll') -Algorithm SHA256).Hash
    $archiveHash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
    if ($report.Version -ne $version -or $report.ContainsSource -ne $false -or
        $report.SHA256 -ne $archiveHash -or $report.DllSHA256 -ne $dllHash -or $info.DllSHA256 -ne $dllHash) {
        throw 'Archive, build and verification report disagree.'
    }

    # Compare the entire ZIP against the current build and the manual-install documentation.
    $expected = @{}
    foreach ($file in Get-ChildItem -LiteralPath $plugin -File -Recurse) {
        $relative = 'plugin/' + $file.FullName.Substring($plugin.Length + 1).Replace('\', '/')
        $expected[$relative] = $file.FullName
    }
    foreach ($name in @('README-RU.txt', 'LICENSE', 'THIRD-PARTY-NOTICES.md')) {
        $expected[$name] = Join-Path $root $name
    }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($archive)
    try {
        $entries = @($zip.Entries | Where-Object { $_.Name.Length -gt 0 })
        if ($entries.Count -ne $expected.Count -or $entries.Count -ne $report.Files) { throw 'Unexpected ZIP file count.' }
        $seen = @{}
        foreach ($entry in $entries) {
            $name = $entry.FullName.Replace('\', '/')
            if (!$expected.ContainsKey($name) -or $seen.ContainsKey($name)) { throw "Unexpected or duplicate ZIP entry: $name" }
            $seen[$name] = $true
            $stream = $entry.Open()
            $sha = [Security.Cryptography.SHA256]::Create()
            try { $entryHash = ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '') }
            finally { $stream.Dispose(); $sha.Dispose() }
            if ($entryHash -ne (Get-FileHash -LiteralPath $expected[$name] -Algorithm SHA256).Hash) {
                throw "ZIP file differs from current build/documentation: $name"
            }
        }
    } finally { $zip.Dispose() }

    Write-Host "Verified $tag at $head; $($entries.Count) files; ZIP SHA256 $archiveHash"
    if ($UpdateExisting) {
        & (Join-Path $PSScriptRoot 'Update-ReleasePlugin.ps1') -ArchivePath $archive -NotesFile $notes -CheckOnly:$CheckOnly
        return
    }
    if ($CheckOnly) {
        Write-Host 'Check complete. No release or tag was created.'
        return
    }

    # Unique local staging preserves all older packages and publication attempts.
    $stage = Join-Path $root ('artifacts/publication/' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
    New-Item -ItemType Directory -Path $stage | Out-Null
    $assetName = "FF14Accessibility-$version-RU-no-source.zip"
    $asset = Join-Path $stage $assetName
    Copy-Item -LiteralPath $archive -Destination $asset
    $sums = Join-Path $stage 'SHA256SUMS.txt'
    $installerAsset = Join-Path $stage 'FF14AccessibilityInstaller.exe'
    $installerSource = Join-Path $stage ([IO.Path]::GetFileName($installer.SourceArchive))
    Copy-Item -LiteralPath $installer.Exe -Destination $installerAsset
    Copy-Item -LiteralPath $installer.SourceArchive -Destination $installerSource
    @("$archiveHash  $assetName", "$dllHash  plugin/FF14Accessibility.dll",
        "$($installer.SHA256)  FF14AccessibilityInstaller.exe", "$($installer.SourceSHA256)  $([IO.Path]::GetFileName($installerSource))") |
        Set-Content -LiteralPath $sums -Encoding ASCII
    # gh uploads all assets before publishing. A failure must be inspected, never overwritten.
    Invoke-ReleaseCommand 'gh' @('release', 'create', $tag, $asset, $sums, $installerAsset, $installerSource,
        '--repo', $repository, '--target', $head, '--title', "FF14 Accessibility $version - RU",
        '--notes-file', $notes, '--latest')
    Write-Host "Published https://github.com/$repository/releases/tag/$tag"
} finally {
    Pop-Location
}
