[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$NotesFile, [switch]$CheckOnly)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Local-Common.ps1')
. (Join-Path $PSScriptRoot 'Installer-Common.ps1')
$root = Get-LocalRoot
$repository = 'pitpamyati-netizen/ff14-accessibility'
function Invoke-InstallerRelease([string[]]$Arguments) {
    $output = & gh @Arguments
    if ($LASTEXITCODE -ne 0) { throw "GitHub command failed: $($Arguments -join ' ')" }
    $output
}
function Read-LatestInstallerRelease {
    (Invoke-InstallerRelease @('api', "repos/$repository/releases/latest") | Out-String) | ConvertFrom-Json
}
function Get-InstallerAssetIdentity($Release) {
    @($Release.assets | Sort-Object name | Select-Object id,name,size,digest,updated_at) | ConvertTo-Json -Depth 20 -Compress
}
function Assert-Assets($Release, [string]$Directory) {
    foreach ($asset in $Release.assets) {
        $path = Join-Path $Directory $asset.name
        if ((Get-Item -LiteralPath $path).Length -ne $asset.size) { throw "Asset size mismatch: $($asset.name)" }
        $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($asset.digest -and $asset.digest -ne "sha256:$hash") { throw "Asset digest mismatch: $($asset.name)" }
    }
}
Assert-LocalVerification $root
$installer = Assert-InstallerBuild $root
$notesPath = (Resolve-Path -LiteralPath $NotesFile).Path
$notes = [IO.File]::ReadAllText($notesPath)
if ([string]::IsNullOrWhiteSpace($notes)) { throw 'Installer release notes are empty.' }
$planPath = Join-Path $root 'artifacts/installer-publication-plan.json'
if ($CheckOnly) {
    $release = Read-LatestInstallerRelease
    if ($release.draft -or $release.prerelease) { throw 'The latest release is not stable.' }
    if (@($release.assets | Where-Object name -eq 'FF14AccessibilityInstaller.exe').Count -ne 1) { throw 'Expected one installer asset in latest release.' }
    $stage = Join-Path $root ('artifacts/publication/installer-' + $installer.Version + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
    $before = Join-Path $stage 'before'
    $next = Join-Path $stage 'next'
    New-Item -ItemType Directory -Path $before,$next | Out-Null
    $release | ConvertTo-Json -Depth 50 | Set-Content -LiteralPath (Join-Path $stage 'before-release.json') -Encoding UTF8
    Invoke-InstallerRelease @('release','download',$release.tag_name,'--repo',$repository,'--dir',$before) | Out-Null
    Assert-Assets $release $before
    Copy-Item -LiteralPath $installer.Exe -Destination (Join-Path $next 'FF14AccessibilityInstaller.exe')
    $sourceName = [IO.Path]::GetFileName($installer.SourceArchive)
    Copy-Item -LiteralPath $installer.SourceArchive -Destination (Join-Path $next $sourceName)
    $sumsPath = Join-Path $before 'SHA256SUMS.txt'
    if (!(Test-Path -LiteralPath $sumsPath)) { throw 'Latest release has no SHA256SUMS.txt.' }
    $sums = @(Get-Content -LiteralPath $sumsPath | Where-Object { $_ -notmatch 'FF14AccessibilityInstaller' })
    $sums += "$($installer.SHA256)  FF14AccessibilityInstaller.exe"
    $sums += "$($installer.SourceSHA256)  $sourceName"
    $sums | Set-Content -LiteralPath (Join-Path $next 'SHA256SUMS.txt') -Encoding ASCII
    $body = $release.body -replace 'FF14AccessibilityInstaller-[0-9.]+-source\.zip', $sourceName
    [IO.File]::WriteAllText((Join-Path $next 'notes.md'),($body.TrimEnd() + "`r`n`r`n" + $notes.Trim() + "`r`n"),[Text.UTF8Encoding]::new($false))
    $plan = [ordered]@{
        ReleaseId=$release.id; Tag=$release.tag_name; Stage=$stage; InstallerSHA256=$installer.SHA256
        SourceSHA256=$installer.SourceSHA256; SourceName=$sourceName
        NotesSHA256=(Get-FileHash -LiteralPath $notesPath -Algorithm SHA256).Hash
        SumsSHA256=(Get-FileHash -LiteralPath (Join-Path $next 'SHA256SUMS.txt') -Algorithm SHA256).Hash
        BodySHA256=(Get-FileHash -LiteralPath (Join-Path $next 'notes.md') -Algorithm SHA256).Hash
    }
    $plan | ConvertTo-Json | Set-Content -LiteralPath $planPath -Encoding UTF8
    Write-Host "Preflight passed. Saved every previous asset: $before"
    Write-Host "Prepared installer $($installer.Version) for existing release $($release.tag_name). No GitHub changes."
    return
}
$plan = Get-Content -LiteralPath $planPath -Raw | ConvertFrom-Json
$beforeRelease = Get-Content -LiteralPath (Join-Path $plan.Stage 'before-release.json') -Raw | ConvertFrom-Json
$current = Read-LatestInstallerRelease
if ($current.id -ne $plan.ReleaseId -or $current.tag_name -ne $plan.Tag -or $current.body -ne $beforeRelease.body -or
    (Get-InstallerAssetIdentity $current) -ne (Get-InstallerAssetIdentity $beforeRelease)) {
    throw 'Latest release changed after preflight. Repeat CheckOnly.'
}
if ($installer.SHA256 -ne $plan.InstallerSHA256 -or $installer.SourceSHA256 -ne $plan.SourceSHA256 -or
    (Get-FileHash -LiteralPath $notesPath -Algorithm SHA256).Hash -ne $plan.NotesSHA256) { throw 'Installer or notes changed after preflight.' }
$next = Join-Path $plan.Stage 'next'
foreach ($pair in @(@('FF14AccessibilityInstaller.exe',$plan.InstallerSHA256),@($plan.SourceName,$plan.SourceSHA256),@('SHA256SUMS.txt',$plan.SumsSHA256),@('notes.md',$plan.BodySHA256))) {
    if ((Get-FileHash -LiteralPath (Join-Path $next $pair[0]) -Algorithm SHA256).Hash -ne $pair[1]) { throw "Prepared asset changed: $($pair[0])" }
}
Invoke-InstallerRelease @('release','upload',$plan.Tag,(Join-Path $next 'FF14AccessibilityInstaller.exe'),(Join-Path $next $plan.SourceName),(Join-Path $next 'SHA256SUMS.txt'),'--repo',$repository,'--clobber') | Out-Null
Invoke-InstallerRelease @('release','edit',$plan.Tag,'--repo',$repository,'--notes-file',(Join-Path $next 'notes.md')) | Out-Null
foreach ($asset in $beforeRelease.assets | Where-Object { $_.name -match '^FF14AccessibilityInstaller-[0-9.]+-source\.zip$' -and $_.name -ne $plan.SourceName }) {
    Invoke-InstallerRelease @('api',"repos/$repository/releases/assets/$($asset.id)",'--method','DELETE') | Out-Null
}
$published = Read-LatestInstallerRelease
if ($published.id -ne $plan.ReleaseId -or $published.draft -or $published.prerelease -or
    $published.body.Trim() -ne [IO.File]::ReadAllText((Join-Path $next 'notes.md')).Trim()) { throw 'Published release metadata mismatch.' }
$after = Join-Path $plan.Stage 'after'
New-Item -ItemType Directory -Path $after | Out-Null
Invoke-InstallerRelease @('release','download',$plan.Tag,'--repo',$repository,'--dir',$after) | Out-Null
Assert-Assets $published $after
foreach ($name in @('FF14AccessibilityInstaller.exe',$plan.SourceName,'SHA256SUMS.txt')) {
    if ((Get-FileHash -LiteralPath (Join-Path $after $name)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $next $name)).Hash) { throw "Round-trip mismatch: $name" }
}
foreach ($asset in $beforeRelease.assets | Where-Object { $_.name -notmatch 'FF14AccessibilityInstaller' -and $_.name -ne 'SHA256SUMS.txt' }) {
    if ((Get-FileHash -LiteralPath (Join-Path $after $asset.name)).Hash -ne (Get-FileHash -LiteralPath (Join-Path (Join-Path $plan.Stage 'before') $asset.name)).Hash) {
        throw "Unrelated release asset changed: $($asset.name)"
    }
}
$published | ConvertTo-Json -Depth 50 | Set-Content -LiteralPath (Join-Path $plan.Stage 'after-release.json') -Encoding UTF8
[ordered]@{ Passed=$true; Release=$published.html_url; Version=$installer.Version; ExeSHA256=$installer.SHA256;
    SourceSHA256=$installer.SourceSHA256; AssetsVerified=$published.assets.Count; PreviousAssetsSaved=$true; InGameVerified=$false } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $plan.Stage 'verification.json') -Encoding UTF8
Write-Host "Installer replaced and all downloaded assets verified: $($published.html_url)"
