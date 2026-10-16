[CmdletBinding()]
param([string]$DalamudHome = $env:DALAMUD_HOME)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Local-Common.ps1')
$root = Get-LocalRoot
if ([string]::IsNullOrWhiteSpace($DalamudHome)) {
    $DalamudHome = Join-Path $env:APPDATA 'XIVLauncher\addon\Hooks\dev'
}
foreach ($name in @('Dalamud.dll', 'FFXIVClientStructs.dll', 'Lumina.dll', 'Lumina.Excel.dll', 'InteropGenerator.Runtime.dll')) {
    if (!(Test-Path -LiteralPath (Join-Path $DalamudHome $name))) { throw "Dalamud dependency is missing: $name in $DalamudHome" }
}
$version = Get-LocalVersion $root
$pluginSource = Get-Content -LiteralPath (Join-Path $root 'FF14Accessibility\Plugin.cs') -Raw
if ($pluginSource -notmatch ('PluginVersion\s*=\s*"' + [regex]::Escape($version) + '"')) {
    throw 'Plugin.cs and the project version disagree.'
}
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$build = Join-Path $root "artifacts\build\$stamp"
$logs = Join-Path $root 'artifacts\logs'
New-Item -ItemType Directory -Force -Path $build, $logs | Out-Null
$fingerprint = Get-LocalSourceFingerprint $root
$projectPath = Join-Path $root 'FF14Accessibility\FF14Accessibility.csproj'
& dotnet build $projectPath -c Release --no-incremental -warnaserror --output $build "-p:DALAMUD_HOME=$DalamudHome" -v:minimal 2>&1 |
    Tee-Object -FilePath (Join-Path $logs "build-$stamp.txt") | Write-Host
if ($LASTEXITCODE -ne 0) { throw "Build failed: exit $LASTEXITCODE" }
if ((Get-LocalSourceFingerprint $root) -ne $fingerprint) { throw 'Source changed during the build. Build again.' }

$stage = Join-Path $root "artifacts\staging\$stamp\plugin"
New-Item -ItemType Directory -Force -Path $stage | Out-Null
Get-ChildItem -LiteralPath $build -File | Where-Object {
    $_.Extension -eq '.dll' -or $_.Name -in @('FF14Accessibility.json', 'FF14Accessibility.deps.json', 'LICENSE', 'THIRD-PARTY-NOTICES.md')
} | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $stage }
foreach ($name in @('assets', 'runtimes')) {
    Copy-Item -LiteralPath (Join-Path $build $name) -Destination $stage -Recurse
}
Assert-LocalPlugin $stage $version
$destination = Join-Path $root 'artifacts\plugin'
if (Test-Path -LiteralPath $destination) {
    $history = Join-Path $root "artifacts\history\$stamp"
    New-Item -ItemType Directory -Force -Path $history | Out-Null
    # Both resolved move targets are fixed children of this workspace's artifacts directory.
    if ([IO.Path]::GetFullPath($destination) -ne (Join-Path $root 'artifacts\plugin') -or
        ![IO.Path]::GetFullPath($history).StartsWith((Join-Path $root 'artifacts\history') + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Unexpected build archive paths.'
    }
    Move-Item -LiteralPath $destination -Destination (Join-Path $history 'plugin')
}
if (![IO.Path]::GetFullPath($stage).StartsWith((Join-Path $root 'artifacts\staging') + '\', [StringComparison]::OrdinalIgnoreCase) -or
    [IO.Path]::GetFullPath($destination) -ne (Join-Path $root 'artifacts\plugin')) { throw 'Unexpected staging paths.' }
Move-Item -LiteralPath $stage -Destination $destination
$report = [ordered]@{
    Version = $version
    BuiltAt = (Get-Date).ToString('o')
    SourceFingerprint = $fingerprint
    DllSHA256 = (Get-FileHash -LiteralPath (Join-Path $destination 'FF14Accessibility.dll')).Hash
    FileCount = @(Get-ChildItem -LiteralPath $destination -File -Recurse).Count
    InstalledIntoGame = $false
}
$report | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'artifacts\build-info.json') -Encoding UTF8
Write-Host "Release $version prepared in $destination"
Write-Host 'The build has not changed the installed plugin.'
& (Join-Path $PSScriptRoot 'Build-Installer.ps1')
