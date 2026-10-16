[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ArchivePath,
      [Parameter(Mandatory=$true)][string]$NotesFile, [switch]$CheckOnly)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Local-Common.ps1')
. (Join-Path $PSScriptRoot 'Installer-Common.ps1')
$root = Get-LocalRoot
$repository = 'pitpamyati-netizen/ff14-accessibility'
function Invoke-PluginRelease([string[]]$Arguments) {
    $output = & gh @Arguments
    if ($LASTEXITCODE -ne 0) { throw "GitHub command failed: $($Arguments -join ' ')" }
    $output
}
function Read-LatestPluginRelease {
    (Invoke-PluginRelease @('api', "repos/$repository/releases/latest") | Out-String) | ConvertFrom-Json
}
function AssetIdentity($release) {
    @($release.assets | Sort-Object name | Select-Object id,name,size,digest,updated_at) | ConvertTo-Json -Depth 20 -Compress
}
function Assert-DownloadedAssets($release, [string]$directory) {
    foreach ($asset in $release.assets) {
        $path = Join-Path $directory $asset.name
        $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        if ((Get-Item -LiteralPath $path).Length -ne $asset.size -or
            ($asset.digest -and $asset.digest -ne "sha256:$hash")) { throw "Asset mismatch: $($asset.name)" }
    }
}
Assert-LocalVerification $root
$archive = (Resolve-Path -LiteralPath $ArchivePath).Path
$notes = (Resolve-Path -LiteralPath $NotesFile).Path
$version = Get-LocalVersion $root
$head = (& git -C $root rev-parse HEAD | Out-String).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot read HEAD.' }
$tag = "v$version"
$archiveHash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
$notesHash = (Get-FileHash -LiteralPath $notes -Algorithm SHA256).Hash
$planPath = Join-Path $root 'artifacts/plugin-publication-plan.json'
if ($CheckOnly) {
    $release = Read-LatestPluginRelease
    if ($release.draft -or $release.prerelease -or
        [version]$release.tag_name.TrimStart('v') -ge [version]$version) { throw 'Expected an older stable latest release.' }
    $oldPlugin = @($release.assets | Where-Object name -match '^FF14Accessibility-[0-9.]+-RU-no-source\.zip$')
    if ($oldPlugin.Count -ne 1) { throw 'Expected exactly one previous plugin archive.' }
    $stage = Join-Path $root ('artifacts/publication/plugin-update-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
    $before = Join-Path $stage 'before'
    $next = Join-Path $stage 'next'
    New-Item -ItemType Directory -Path $before,$next | Out-Null
    $release | ConvertTo-Json -Depth 50 | Set-Content -LiteralPath (Join-Path $stage 'before-release.json') -Encoding UTF8
    Invoke-PluginRelease @('release','download',$release.tag_name,'--repo',$repository,'--dir',$before) | Out-Null
    Assert-DownloadedAssets $release $before
    $assetName = "FF14Accessibility-$version-RU-no-source.zip"
    Copy-Item -LiteralPath $archive -Destination (Join-Path $next $assetName)
    # Preserve the published installer, its source and any unrelated assets.
    $sums = @(Get-Content -LiteralPath (Join-Path $before 'SHA256SUMS.txt') |
        Where-Object { $_ -notmatch 'FF14Accessibility-[0-9.]+-RU-no-source\.zip|plugin/FF14Accessibility\.dll' })
    $dllHash = (Get-FileHash -LiteralPath (Join-Path $root 'artifacts/plugin/FF14Accessibility.dll') -Algorithm SHA256).Hash
    $sums += "$archiveHash  $assetName"
    $sums += "$dllHash  plugin/FF14Accessibility.dll"
    $sums | Set-Content -LiteralPath (Join-Path $next 'SHA256SUMS.txt') -Encoding ASCII
    Copy-Item -LiteralPath $notes -Destination (Join-Path $next 'notes.md')
    [ordered]@{ ReleaseId=$release.id; PreviousTag=$release.tag_name; Tag=$tag; Head=$head; Stage=$stage;
        ArchiveSHA256=$archiveHash; NotesSHA256=$notesHash;
        SumsSHA256=(Get-FileHash -LiteralPath (Join-Path $next 'SHA256SUMS.txt')).Hash; AssetName=$assetName } |
        ConvertTo-Json | Set-Content -LiteralPath $planPath -Encoding UTF8
    Write-Host "Preflight passed. All previous assets saved: $before"
    Write-Host "Prepared $version as an update of release ID $($release.id). No GitHub changes."
    return
}
$plan = Get-Content -LiteralPath $planPath -Raw | ConvertFrom-Json
$beforeRelease = Get-Content -LiteralPath (Join-Path $plan.Stage 'before-release.json') -Raw | ConvertFrom-Json
$current = Read-LatestPluginRelease
if ($current.id -ne $plan.ReleaseId -or $current.tag_name -ne $plan.PreviousTag -or
    $current.body -ne $beforeRelease.body -or (AssetIdentity $current) -ne (AssetIdentity $beforeRelease)) {
    throw 'Latest release changed after preflight. Repeat CheckOnly.'
}
if ($tag -ne $plan.Tag -or $head -ne $plan.Head -or $archiveHash -ne $plan.ArchiveSHA256 -or $notesHash -ne $plan.NotesSHA256) {
    throw 'Prepared commit, archive or notes changed after preflight.'
}
$next = Join-Path $plan.Stage 'next'
foreach ($pair in @(@($plan.AssetName,$plan.ArchiveSHA256),@('SHA256SUMS.txt',$plan.SumsSHA256),@('notes.md',$plan.NotesSHA256))) {
    if ((Get-FileHash -LiteralPath (Join-Path $next $pair[0])).Hash -ne $pair[1]) { throw "Prepared file changed: $($pair[0])" }
}
Invoke-PluginRelease @('release','upload',$plan.PreviousTag,(Join-Path $next $plan.AssetName),
    (Join-Path $next 'SHA256SUMS.txt'),'--repo',$repository,'--clobber') | Out-Null
# Switch the EXISTING release to a new tag/version. The old tag is preserved;
# GitHub Source code now corresponds to the fixed build, without rewriting tags.
Invoke-PluginRelease @('release','edit',$plan.PreviousTag,'--repo',$repository,'--tag',$tag,'--target',$head,
    '--title',"FF14 Accessibility $version - RU",'--notes-file',(Join-Path $next 'notes.md'),'--latest') | Out-Null
foreach ($asset in $beforeRelease.assets | Where-Object name -match '^FF14Accessibility-[0-9.]+-RU-no-source\.zip$') {
    Invoke-PluginRelease @('api',"repos/$repository/releases/assets/$($asset.id)",'--method','DELETE') | Out-Null
}
$published = Read-LatestPluginRelease
if ($published.id -ne $plan.ReleaseId -or $published.tag_name -ne $tag -or $published.draft -or $published.prerelease -or
    $published.body.Trim() -ne [IO.File]::ReadAllText((Join-Path $next 'notes.md')).Trim()) { throw 'Release metadata mismatch.' }
$after = Join-Path $plan.Stage 'after'
New-Item -ItemType Directory -Path $after | Out-Null
Invoke-PluginRelease @('release','download',$tag,'--repo',$repository,'--dir',$after) | Out-Null
Assert-DownloadedAssets $published $after
foreach ($name in @($plan.AssetName,'SHA256SUMS.txt')) {
    if ((Get-FileHash -LiteralPath (Join-Path $after $name)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $next $name)).Hash) {
        throw "Round-trip mismatch: $name"
    }
}
foreach ($asset in $beforeRelease.assets | Where-Object { $_.name -ne 'SHA256SUMS.txt' -and $_.name -notmatch '^FF14Accessibility-[0-9.]+-RU-no-source\.zip$' }) {
    if ((Get-FileHash -LiteralPath (Join-Path $after $asset.name)).Hash -ne
        (Get-FileHash -LiteralPath (Join-Path (Join-Path $plan.Stage 'before') $asset.name)).Hash) { throw "Unrelated asset changed: $($asset.name)" }
}
$published | ConvertTo-Json -Depth 50 | Set-Content -LiteralPath (Join-Path $plan.Stage 'after-release.json') -Encoding UTF8
[ordered]@{ Passed=$true; ReleaseId=$published.id; PreviousTag=$plan.PreviousTag; Tag=$tag; Head=$head;
    AssetsVerified=$published.assets.Count; PreviousAssetsSaved=$true; InGameVerified=$false } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $plan.Stage 'verification.json') -Encoding UTF8
Write-Host "Existing release updated and all downloaded assets verified: $($published.html_url)"
