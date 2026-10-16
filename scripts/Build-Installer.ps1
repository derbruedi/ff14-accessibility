[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Installer-Common.ps1')
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$fingerprint = Get-InstallerFingerprint $root
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$output = Join-Path $root "artifacts/installer/$stamp"
$logs = Join-Path $root 'artifacts/logs'
New-Item -ItemType Directory -Force -Path $output, $logs | Out-Null
& dotnet publish (Join-Path $root 'Installer/Russian/FF14AccessibilityInstaller.Ru.csproj') -c Release -warnaserror -r win-x64 --self-contained true --output $output -v:minimal 2>&1 |
    Tee-Object -FilePath (Join-Path $logs "installer-build-$stamp.txt") | Write-Host
if ($LASTEXITCODE -ne 0) { throw 'Installer publish failed.' }
if ((Get-InstallerFingerprint $root) -ne $fingerprint) { throw 'Installer sources changed during publish.' }
$exe = Join-Path $output 'FF14AccessibilityInstaller.exe'
[xml]$project = Get-Content -LiteralPath (Join-Path $root 'Installer/Russian/FF14AccessibilityInstaller.Ru.csproj') -Raw
$expectedVersion = [string]$project.Project.PropertyGroup.FileVersion
$packageVersion = [string]$project.Project.PropertyGroup.Version
$version = [Diagnostics.FileVersionInfo]::GetVersionInfo($exe).FileVersion
if ($version -ne $expectedVersion) { throw "Unexpected installer executable version: $version" }
if (@(Get-ChildItem -LiteralPath $output -File).Count -ne 1) { throw 'Expected exactly one standalone executable.' }

# Separate source archive makes the extra installer asset reproducible even when attached to an existing plugin release.
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$sourceZip = Join-Path $output "FF14AccessibilityInstaller-$packageVersion-source.zip"
$zip = [IO.Compression.ZipFile]::Open($sourceZip, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in Get-InstallerSourceFiles $root) {
        $entry = $file.Substring($root.Length + 1).Replace('\', '/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file, $entry, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $zip.Dispose() }
$zip = [IO.Compression.ZipFile]::OpenRead($sourceZip)
try {
    foreach ($entry in $zip.Entries) {
        $stream = $entry.Open()
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $hash = ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '') }
        finally { $stream.Dispose(); $sha.Dispose() }
        if ($hash -ne (Get-FileHash -LiteralPath (Join-Path $root $entry.FullName) -Algorithm SHA256).Hash) { throw 'Installer source ZIP mismatch.' }
    }
    $sourceCount = $zip.Entries.Count
} finally { $zip.Dispose() }
$report = [ordered]@{
    Version = $version; Exe = $exe; SHA256 = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
    Bytes = (Get-Item -LiteralPath $exe).Length; SourceFingerprint = $fingerprint
    SourceArchive = $sourceZip; SourceSHA256 = (Get-FileHash -LiteralPath $sourceZip -Algorithm SHA256).Hash
    SourceFiles = $sourceCount; BuiltAt = (Get-Date).ToString('o')
}
$report | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'artifacts/installer-build-info.json') -Encoding UTF8
$report | ConvertTo-Json | Write-Host
