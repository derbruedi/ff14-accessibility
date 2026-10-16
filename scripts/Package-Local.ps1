[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Local-Common.ps1')
$root = Get-LocalRoot
Assert-LocalVerification $root
$version = Get-LocalVersion $root
$plugin = Join-Path $root 'artifacts\plugin'
$info = Get-Content -LiteralPath (Join-Path $root 'artifacts\build-info.json') -Raw | ConvertFrom-Json
if ((Get-LocalSourceFingerprint $root) -ne $info.SourceFingerprint) { throw 'Source changed since the build. Run Build-Local.ps1 first.' }
Assert-LocalPlugin $plugin $version
if ((Get-FileHash -LiteralPath (Join-Path $plugin 'FF14Accessibility.dll')).Hash -ne $info.DllSHA256) {
    throw 'Prepared plugin differs from the verified build.'
}
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$stage = Join-Path $root "artifacts\packages\$stamp"
$releases = Join-Path $root 'artifacts\releases'
New-Item -ItemType Directory -Force -Path $stage, $releases | Out-Null
Copy-Item -LiteralPath $plugin -Destination (Join-Path $stage 'plugin') -Recurse
foreach ($name in @('README-RU.txt', 'LICENSE', 'THIRD-PARTY-NOTICES.md')) {
    Copy-Item -LiteralPath (Join-Path $root $name) -Destination $stage
}
$zipPath = Join-Path $releases "FF14Accessibility-$version-RU-no-source.zip"
if (Test-Path -LiteralPath $zipPath) { $zipPath = Join-Path $releases "FF14Accessibility-$version-RU-no-source-$stamp.zip" }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($stage, $zipPath, [IO.Compression.CompressionLevel]::Optimal, $false)
$zip = [IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $entries = @($zip.Entries | Where-Object { $_.Name.Length -gt 0 })
    $forbidden = @($entries | Where-Object { $_.FullName -match '(?i)(^|/)(bin|obj|\.git)/|\.(cs|csproj|sln|pdb|log|bak|bat|cmd|ps1|psm1|vbs|exe)$' })
    if ($forbidden.Count) { throw "Forbidden archive entries: $($forbidden.FullName -join ', ')" }
    $expectedFiles = @(Get-ChildItem -LiteralPath $stage -File -Recurse)
    if ($entries.Count -ne $expectedFiles.Count) { throw 'Archive file count differs from staging.' }
    $names = @($entries.FullName)
    if (@($names | Sort-Object -Unique).Count -ne $names.Count) { throw 'Duplicate ZIP entries.' }
    foreach ($entry in $entries) {
        $relative = $entry.FullName.Replace('/', '\')
        $file = Join-Path $stage $relative
        if (!(Test-Path -LiteralPath $file -PathType Leaf)) { throw "Unexpected archive entry: $relative" }
        $stream = $entry.Open()
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $hash = ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '') }
        finally { $stream.Dispose(); $sha.Dispose() }
        if ($hash -ne (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash) { throw "ZIP hash mismatch: $relative" }
    }
} finally { $zip.Dispose() }
$report = [ordered]@{
    Version = $version
    Archive = $zipPath
    ContainsSource = $false
    ManualInstall = $true
    ContainsInstallScripts = $false
    Files = $entries.Count
    Bytes = (Get-Item -LiteralPath $zipPath).Length
    SHA256 = (Get-FileHash -LiteralPath $zipPath).Hash
    DllSHA256 = $info.DllSHA256
}
$report | ConvertTo-Json | Set-Content -LiteralPath ($zipPath + '.verification.json') -Encoding UTF8
$report | ConvertTo-Json | Write-Host
